// X Timeline Translator - コンテンツスクリプト
// 投稿の検知、同意確認、翻訳結果のインライン表示を行います。

(function () {
    if (window._xtvTranslatorLoaded) return;
    window._xtvTranslatorLoaded = true;

    const translationConsentKey = 'xtv_translation_external_consent_v1';
    const autoTranslateKey = 'xtv_auto_translate';
    const translationProviderKey = 'xtv_translation_provider_v1';
    const defaultTranslationProvider = 'google';
    const supportedTranslationProviders = Object.freeze(['google', 'disabled']);
    const supportedTargetLanguages = Object.freeze(['ja', 'en']);
    const translationCacheLimit = 128;
    const maxTranslationChars = 10000;
    const scanDebounceMs = 120;
    const translationTimeoutMs = 12000;
    let retryTimer = null;
    let retryDeadline = 0;
    let translationConsent = false;
    let autoTranslateEnabled = false;
    let translationProvider = defaultTranslationProvider;
    let pendingConsent = null;
    let scanTimer = null;
    let scanRequested = false;
    const translationCache = new Map();
    const tweetStates = new WeakMap();

    const ui = {
        ja: {
            brand: 'XTimelineViewer Kotsume Edition',
            consentTitle: '翻訳時の外部送信について',
            consentBody: '翻訳する投稿本文を、Google の翻訳用エンドポイントへ送信します。投稿に個人情報や秘密情報が含まれる場合は翻訳しないでください。',
            consentNote: '同意は右上の「同意設定」からいつでも取り消せます。この確認はXではなく、本アプリが表示しています。',
            appPrivacy: 'アプリのプライバシーポリシー',
            googlePrivacy: 'Googleのプライバシーポリシー',
            cancel: '今回は使わない',
            accept: '同意して翻訳する',
            settingsTitle: '翻訳設定',
            status: value => value ? '同意済み' : '未同意',
            settingsNote: '同意を取り消すと自動翻訳もOFFになり、次回の翻訳時にもう一度説明を表示します。',
            providerStatus: value => value ? 'Google方式：有効' : 'Google方式：無効',
            providerNote: 'Google方式を無効にすると、この拡張機能からGoogleへ投稿本文を送信しません。別の翻訳拡張を使う場合にも利用できます。',
            disableGoogle: 'Google方式を無効にする',
            enableGoogle: 'Google方式を有効にする',
            close: '閉じる',
            revoke: '同意を取り消す',
            translating: '翻訳中...',
            waiting: '翻訳の順番を待っています',
            rateLimited: '翻訳先の通信制限で休止中です。時間を空けて再開します。',
            failed: '翻訳できませんでした。通信状態を確認して再試行してください。',
            unavailable: '翻訳機能を利用できません。アプリを再起動してください。',
            providerDisabled: 'Google方式は無効です。別の翻訳拡張を使うか、設定で有効にしてください。',
            tooLong: '投稿が長すぎるため翻訳できません。',
            retry: '翻訳を再試行',
            hide: '翻訳を非表示',
            show: '翻訳を表示',
            from: value => `${value}からの翻訳`,
            unknownLanguage: '外国語',
            languages: { en: '英語', 'zh-CN': '中国語(簡体字)', 'zh-TW': '中国語(繁体字)', ko: '韓国語', fr: 'フランス語', de: 'ドイツ語', es: 'スペイン語', ru: 'ロシア語', it: 'イタリア語', pt: 'ポルトガル語', auto: '外国語' }
        },
        en: {
            brand: 'XTimelineViewer Kotsume Edition',
            consentTitle: 'About sending text for translation',
            consentBody: 'The post text you translate will be sent to Google\'s translation endpoint. Do not translate posts containing personal or confidential information.',
            consentNote: 'You can withdraw consent at any time from “Consent settings”. This dialog is shown by the app, not by X.',
            appPrivacy: 'App privacy policy',
            googlePrivacy: 'Google privacy policy',
            cancel: 'Not now',
            accept: 'Agree and translate',
            settingsTitle: 'Translation settings',
            status: value => value ? 'Consent given' : 'No consent',
            settingsNote: 'Withdrawing consent also turns off automatic translation. You will see this explanation again before the next translation.',
            providerStatus: value => value ? 'Google method: enabled' : 'Google method: disabled',
            providerNote: 'When the Google method is disabled, this extension does not send post text to Google. You can use another translation extension instead.',
            disableGoogle: 'Disable Google method',
            enableGoogle: 'Enable Google method',
            close: 'Close',
            revoke: 'Withdraw consent',
            translating: 'Translating...',
            waiting: 'Waiting for translation',
            rateLimited: 'Translation is rate limited. It will resume after a pause.',
            failed: 'Translation failed. Check your connection and try again.',
            unavailable: 'Translation is unavailable. Please restart the app.',
            providerDisabled: 'The Google method is disabled. Use another translation extension or enable it in settings.',
            tooLong: 'This post is too long to translate.',
            retry: 'Retry translation',
            hide: 'Hide translation',
            show: 'Show translation',
            from: value => `Translated from ${value}`,
            unknownLanguage: 'another language',
            languages: { en: 'English', 'zh-CN': 'Simplified Chinese', 'zh-TW': 'Traditional Chinese', ko: 'Korean', fr: 'French', de: 'German', es: 'Spanish', ru: 'Russian', it: 'Italian', pt: 'Portuguese', auto: 'another language' }
        }
    };

    function getLocale() {
        const value = String(document.documentElement?.getAttribute('data-xtv-translation-language') || window.navigator?.language || '').toLowerCase();
        return value.startsWith('en') ? 'en' : 'ja';
    }

    function getTargetLanguage() {
        const requested = getLocale();
        return supportedTargetLanguages.includes(requested) ? requested : 'ja';
    }

    function getChrome() {
        return typeof chrome === 'undefined' ? null : chrome;
    }

    function normalizeTranslationProvider(value) {
        return supportedTranslationProviders.includes(value) ? value : defaultTranslationProvider;
    }

    // 同意と自動翻訳の状態は X の localStorage ではなく、拡張機能専用の chrome.storage.local に保存します。
    function readStoredSettings() {
        return new Promise((resolve) => {
            const api = getChrome();
            if (!api?.storage?.local) {
                resolve({ consent: false, auto: false, provider: defaultTranslationProvider });
                return;
            }
            api.storage.local.get([translationConsentKey, autoTranslateKey, translationProviderKey], (result) => {
                if (api.runtime?.lastError) {
                    resolve({ consent: false, auto: false, provider: defaultTranslationProvider });
                    return;
                }
                const consent = result?.[translationConsentKey] === true;
                resolve({
                    consent,
                    auto: consent && result?.[autoTranslateKey] === true,
                    provider: normalizeTranslationProvider(result?.[translationProviderKey])
                });
            });
        });
    }

    function writeStoredSettings(values) {
        return new Promise((resolve, reject) => {
            const api = getChrome();
            if (!api?.storage?.local) {
                reject(new Error('拡張機能の安全な設定ストレージを利用できません。'));
                return;
            }
            api.storage.local.set(values, () => {
                if (api.runtime?.lastError) reject(new Error(api.runtime.lastError.message));
                else resolve();
            });
        });
    }

    let translationHealth = 'ready';
    function reportTranslationHealth(result) {
        if (result?.text) translationHealth = 'ready';
        else if (result?.code === 'rate_limited') translationHealth = 'rate_limited';
        else if (result?.code === 'unavailable') translationHealth = 'unavailable';
        else if (['network', 'timeout', 'http', 'response', 'source'].includes(result?.code)) translationHealth = 'error';
        publishTranslationState();
    }

    function publishTranslationState() {
        document.documentElement.setAttribute(
            'data-xtv-translation-state',
            translationProvider === 'google' && autoTranslateEnabled ? 'on' : 'off');
        document.documentElement.setAttribute('data-xtv-translation-health',
            translationProvider !== 'google' ? 'disabled' : translationHealth);
    }

    function clearInjectedUi(tweet) {
        tweet.querySelectorAll?.('.xtv-translation-box, .xtv-manual-btn, .xtv-translation-status').forEach(el => el.remove());
    }

    function refreshTranslationUi() {
        publishTranslationState();
        if (retryTimer !== null) { clearTimeout(retryTimer); retryTimer = null; }
        document.querySelectorAll('.xtv-translation-box, .xtv-manual-btn, .xtv-translation-status').forEach(el => el.remove());
        document.querySelectorAll('article[data-testid="tweet"]').forEach(tweet => {
            const state = tweetStates.get(tweet);
            if (state) {
                state.generation++;
                state.processing = false;
                state.rawText = '';
                state.noTranslation = false;
                state.retryAt = 0;
                state.pendingManual = false;
                state.visibleSince = 0;
            }
        });
        scheduleScan(0);
    }

    function requestTranslationConsent() {
        if (translationProvider !== 'google') return Promise.resolve(false);
        if (translationConsent) return Promise.resolve(true);
        if (pendingConsent) return pendingConsent;
        const text = ui[getLocale()];
        pendingConsent = new Promise((resolve) => {
            const overlay = document.createElement('div');
            overlay.className = 'xtv-consent-overlay';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');
            overlay.setAttribute('aria-labelledby', 'xtv-consent-title');
            overlay.innerHTML = `
                <div class="xtv-consent-card">
                    <div class="xtv-consent-brand">${text.brand}</div>
                    <h2 id="xtv-consent-title">${text.consentTitle}</h2>
                    <p>${text.consentBody}</p>
                    <p class="xtv-consent-note">${text.consentNote}</p>
                    <div class="xtv-consent-links">
                        <a class="xtv-consent-link" href="https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md" target="_blank" rel="noopener noreferrer">${text.appPrivacy}</a>
                        <a class="xtv-consent-link" href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">${text.googlePrivacy}</a>
                    </div>
                    <div class="xtv-consent-actions">
                        <button type="button" class="xtv-consent-cancel">${text.cancel}</button>
                        <button type="button" class="xtv-consent-accept">${text.accept}</button>
                    </div>
                </div>`;

            const finish = async (accepted) => {
                if (accepted) {
                    try {
                        await writeStoredSettings({ [translationConsentKey]: true, [autoTranslateKey]: false });
                        translationConsent = true;
                    } catch (_) {
                        accepted = false;
                    }
                }
                overlay.remove();
                pendingConsent = null;
                resolve(accepted);
            };

            overlay.addEventListener('click', (event) => {
                event.stopPropagation();
                if (event.target === overlay) finish(false);
            });
            overlay.querySelector('.xtv-consent-cancel')?.addEventListener('click', () => finish(false));
            overlay.querySelector('.xtv-consent-accept')?.addEventListener('click', () => finish(true));
            overlay.addEventListener('keydown', (event) => {
                if (event.key === 'Escape') finish(false);
            });
            (document.body || document.documentElement).appendChild(overlay);
            overlay.querySelector('.xtv-consent-accept')?.focus();
        });
        return pendingConsent;
    }

    function isVisible(tweet) {
        if (document.hidden) return false;
        if (!tweet.getBoundingClientRect) return true;
        const rect = tweet.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0 && rect.bottom > 0 && rect.right > 0 &&
            rect.top < window.innerHeight && rect.left < window.innerWidth;
    }

    function isJapaneseOnly(text) {
        // Kana alone must not suppress a mixed-language post or Japanese → English.
        return /[\p{Script=Hiragana}\p{Script=Katakana}]/u.test(text) &&
            !/\p{Letter}/u.test(text.replace(/[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}ー]/gu, ''));
    }

    function retryScanAt(deadline) {
        if (retryTimer !== null && retryDeadline <= deadline) return;
        if (retryTimer !== null) clearTimeout(retryTimer);
        retryDeadline = deadline;
        retryTimer = setTimeout(() => {
            retryTimer = null;
            scheduleScan(0);
        }, Math.min(2147483647, Math.max(0, deadline - Date.now())));
    }

    function showStatus(tweet, textEl, message) {
        let status = tweet.querySelector('.xtv-translation-status');
        if (!status) {
            status = document.createElement('div');
            status.className = 'xtv-translation-status';
            status.setAttribute('role', 'status');
            textEl.insertAdjacentElement('afterend', status);
        }
        if (status.textContent !== message) status.textContent = message;
    }

    function cacheKey(targetLang, text) {
        return `${targetLang}\u0000${text}`;
    }

    function cacheGet(targetLang, text) {
        const key = cacheKey(targetLang, text);
        if (!translationCache.has(key)) return undefined;
        const value = translationCache.get(key);
        translationCache.delete(key);
        translationCache.set(key, value);
        return value;
    }

    function cacheSet(targetLang, text, value) {
        const key = cacheKey(targetLang, text);
        translationCache.delete(key);
        translationCache.set(key, value);
        while (translationCache.size > translationCacheLimit) {
            translationCache.delete(translationCache.keys().next().value);
        }
    }

    async function requestTranslation(text, targetLang = getTargetLanguage()) {
        const cached = cacheGet(targetLang, text);
        if (cached !== undefined) return cached;
        if (translationProvider !== 'google') return { text: null, lang: '', code: 'provider_disabled' };
        if (!translationConsent) return { text: null, lang: '', code: 'consent' };
        if (typeof text !== 'string' || text.length > maxTranslationChars) {
            return { text: null, lang: '', code: 'input' };
        }
        const api = getChrome();
        if (!api?.runtime?.sendMessage) {
            const result = { text: null, lang: '', code: 'unavailable' };
            reportTranslationHealth(result);
            return result;
        }

        return new Promise((resolve) => {
            let completed = false;
            let timeoutId;
            const finish = (result) => {
                if (completed) return;
                completed = true;
                clearTimeout(timeoutId);
                reportTranslationHealth(result);
                resolve(result);
            };
            timeoutId = setTimeout(() => finish({ text: null, lang: '', error: 'Translation timed out.', code: 'timeout' }), translationTimeoutMs);
            try { api.runtime.sendMessage({ action: 'translate', text, targetLang }, (response) => {
                if (completed) return;
                if (api.runtime.lastError || !response?.success) {
                    finish({ text: null, lang: '', error: response?.error || 'Translation failed.', code: response?.code || 'network', retryAt: response?.retryAt || 0 });
                    return;
                }
                const result = { text: response.translatedText, lang: response.detectedLang };
                if (translationConsent) cacheSet(targetLang, text, result);
                finish(result);
            }); } catch (_) { finish({ text: null, lang: '', code: 'unavailable' }); }
        });
    }

    function getState(tweet) {
        let state = tweetStates.get(tweet);
        if (!state) {
            state = { generation: 0, rawText: '', processing: false, noTranslation: false, retryAt: 0, visibleSince: 0, targetLang: '' };
            tweetStates.set(tweet, state);
        }
        return state;
    }

    function isCurrent(tweet, textEl, rawText, generation) {
        const state = tweetStates.get(tweet);
        if (!state || state.generation !== generation || state.rawText !== rawText) return false;
        if (tweet.isConnected === false || textEl.isConnected === false) return false;
        return tweet.querySelector('[data-testid="tweetText"]') === textEl && textEl.innerText.trim() === rawText;
    }

    function languageName(code) {
        const text = ui[getLocale()];
        return text.languages[code] || code || text.unknownLanguage;
    }

    async function applyTranslation(tweet, textEl, rawText, generation) {
        if (!isCurrent(tweet, textEl, rawText, generation)) return;
        const text = ui[getLocale()];
        tweet.querySelector('.xtv-translation-status')?.remove();
        let box = tweet.querySelector('.xtv-translation-box');
        if (!box) {
            box = document.createElement('div');
            box.className = 'xtv-translation-box';
            box.innerHTML = `<div class="xtv-trans-header"><span class="xtv-trans-badge">🌐 ${text.translating}</span></div>`;
            textEl.insertAdjacentElement('afterend', box);
        }

        const targetLang = getTargetLanguage();
        const result = await requestTranslation(rawText, targetLang);
        if (!isCurrent(tweet, textEl, rawText, generation)) return;
        if (!result?.text) {
            box.remove();
            const state = getState(tweet);
            if (result?.code === 'busy' || result?.code === 'rate_limited') {
                state.retryAt = Math.max(Date.now() + 1500, Number(result.retryAt) || 0);
                state.pendingManual = state.pendingManual || !autoTranslateEnabled;
                showStatus(tweet, textEl, result.code === 'busy' ? text.waiting : text.rateLimited);
                retryScanAt(state.retryAt);
            } else if (result?.code === 'provider_disabled') {
                showStatus(tweet, textEl, text.providerDisabled);
            } else {
                showStatus(tweet, textEl, result?.code === 'input' ? text.tooLong :
                    result?.code === 'unavailable' ? text.unavailable : text.failed);
                showTranslateButton(tweet, textEl, rawText, generation, true);
            }
            return;
        }
        getState(tweet).retryAt = 0;
        getState(tweet).pendingManual = false;
        if (result.lang === targetLang && result.text.trim() === rawText) {
            box.remove();
            getState(tweet).noTranslation = true;
            return;
        }
        box.innerHTML = `
            <div class="xtv-trans-header">
                <span class="xtv-trans-badge">🌐 ${escapeHtml(text.from(languageName(result.lang)))}</span>
                <button type="button" class="xtv-trans-hide-btn" title="${text.hide}">✕</button>
            </div>
            <div class="xtv-trans-body">${escapeHtml(result.text)}</div>`;
        box.querySelector('.xtv-trans-hide-btn')?.addEventListener('click', (event) => {
            event.stopPropagation();
            if (!isCurrent(tweet, textEl, rawText, generation)) return;
            box.remove();
            showTranslateButton(tweet, textEl, rawText, generation);
        });
    }

    function showTranslateButton(tweet, textEl, rawText, generation, failed = false) {
        if (!isCurrent(tweet, textEl, rawText, generation) || tweet.querySelector('.xtv-manual-btn, .xtv-translation-box')) return;
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'xtv-manual-btn';
        btn.innerHTML = `🌐 ${failed ? ui[getLocale()].retry : ui[getLocale()].show}`;
        btn.addEventListener('click', async (event) => {
            event.stopPropagation();
            if (!await requestTranslationConsent() || !isCurrent(tweet, textEl, rawText, generation)) return;
            btn.remove();
            const state = getState(tweet);
            state.retryAt = 0;
            state.pendingManual = true;
            state.processing = true;
            await applyTranslation(tweet, textEl, rawText, generation);
            if (isCurrent(tweet, textEl, rawText, generation)) state.processing = false;
        });
        textEl.insertAdjacentElement('afterend', btn);
    }

    async function processTweet(tweet) {
        if (!tweet?.matches?.('article[data-testid="tweet"]')) return;
        if (translationProvider !== 'google') {
            clearInjectedUi(tweet);
            return;
        }
        const textEl = tweet.querySelector('[data-testid="tweetText"]');
        const state = getState(tweet);
        if (!textEl) {
            if (state.rawText !== '') {
                state.rawText = '';
                state.generation++;
                state.processing = false;
                state.noTranslation = false;
                clearInjectedUi(tweet);
            }
            return;
        }
        const rawText = String(textEl.innerText || '').trim();
        if (!rawText || rawText.length < 2) {
            if (state.rawText !== rawText || state.targetLang !== getTargetLanguage()) {
                state.rawText = rawText;
                state.generation++;
                state.processing = false;
                state.noTranslation = false;
                clearInjectedUi(tweet);
            }
            return;
        }
        if (state.rawText !== rawText || state.targetLang !== getTargetLanguage()) {
            state.rawText = rawText;
            state.targetLang = getTargetLanguage();
            state.retryAt = 0;
            state.visibleSince = 0;
            state.pendingManual = false;
            state.generation++;
            state.processing = false;
            state.noTranslation = false;
            clearInjectedUi(tweet);
        }
        const generation = state.generation;
        if (state.processing || tweet.querySelector('.xtv-translation-box, .xtv-manual-btn')) return;
        if (state.noTranslation) return;
        if (!isVisible(tweet)) { state.visibleSince = 0; return; }
        if (autoTranslateEnabled && !state.pendingManual && getTargetLanguage() === 'ja' && isJapaneseOnly(rawText)) {
            showTranslateButton(tweet, textEl, rawText, generation);
            return;
        }
        if (Date.now() < state.retryAt) { retryScanAt(state.retryAt); return; }
        if (autoTranslateEnabled && !state.pendingManual) {
            if (!state.visibleSince) state.visibleSince = Date.now();
            if (Date.now() - state.visibleSince < 400) { retryScanAt(state.visibleSince + 400); return; }
        }
        state.processing = true;
        if ((autoTranslateEnabled || state.pendingManual) && translationConsent) {
            await applyTranslation(tweet, textEl, rawText, generation);
        } else if (!autoTranslateEnabled) {
            showTranslateButton(tweet, textEl, rawText, generation);
        }
        if (isCurrent(tweet, textEl, rawText, generation)) state.processing = false;
    }

    function scanAndProcess() {
        scanRequested = false;
        document.querySelectorAll('article[data-testid="tweet"]').forEach(tweet => { void processTweet(tweet); });
    }

    function scheduleScan(delay = scanDebounceMs) {
        scanRequested = true;
        if (scanTimer !== null) return;
        scanTimer = setTimeout(() => {
            scanTimer = null;
            if (scanRequested) scanAndProcess();
        }, delay);
    }

    function showConsentSettings() {
        if (document.querySelector('.xtv-consent-overlay')) return;
        const text = ui[getLocale()];
        const googleEnabled = translationProvider === 'google';
        const overlay = document.createElement('div');
        overlay.className = 'xtv-consent-overlay';
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-modal', 'true');
        overlay.setAttribute('aria-labelledby', 'xtv-settings-title');
        overlay.innerHTML = `
            <div class="xtv-consent-card">
                <div class="xtv-consent-brand">${text.brand}</div>
                <h2 id="xtv-settings-title">${text.settingsTitle}</h2>
                <p>${text.status(translationConsent)}</p>
                <p>${text.providerStatus(googleEnabled)}</p>
                <p class="xtv-consent-note">${text.settingsNote}</p>
                <p class="xtv-consent-note">${text.providerNote}</p>
                <p class="xtv-consent-note">${getLocale() === 'ja' ? 'この設定は同じアカウントのタイムラインに共有されます。' : 'These settings apply to timelines using the same account.'}</p>
                <div class="xtv-consent-links">
                    <a class="xtv-consent-link" href="https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md" target="_blank" rel="noopener noreferrer">${text.appPrivacy}</a>
                    <a class="xtv-consent-link" href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">${text.googlePrivacy}</a>
                </div>
                <div class="xtv-consent-actions">
                    <button type="button" class="xtv-consent-cancel">${text.close}</button>
                    <button type="button" class="xtv-consent-provider">${googleEnabled ? text.disableGoogle : text.enableGoogle}</button>
                    ${translationConsent ? `<button type="button" class="xtv-consent-revoke">${text.revoke}</button>` : ''}
                </div>
            </div>`;
        const close = () => overlay.remove();
        overlay.addEventListener('click', (event) => {
            event.stopPropagation();
            if (event.target === overlay) close();
        });
        overlay.querySelector('.xtv-consent-cancel')?.addEventListener('click', close);
        overlay.querySelector('.xtv-consent-provider')?.addEventListener('click', async () => {
            const nextProvider = googleEnabled ? 'disabled' : 'google';
            try {
                const changes = {
                    [translationProviderKey]: nextProvider,
                    [autoTranslateKey]: false,
                };
                // Google方式を無効にした場合は、以前の同意も一緒に取り消す。
                // 再び有効にしたときに、投稿本文の送信について再確認できるようにする。
                if (nextProvider === 'disabled') changes[translationConsentKey] = false;
                await writeStoredSettings(changes);
                translationProvider = nextProvider;
                autoTranslateEnabled = false;
                if (nextProvider === 'disabled') {
                    translationConsent = false;
                    translationCache.clear();
                }
                close();
                refreshTranslationUi();
            } catch (_) { /* 保存に失敗したときは表示状態を変えない */ }
        });
        overlay.querySelector('.xtv-consent-revoke')?.addEventListener('click', async () => {
            try {
                await writeStoredSettings({ [translationConsentKey]: false, [autoTranslateKey]: false });
                translationConsent = false;
                autoTranslateEnabled = false;
                translationCache.clear();
                close();
                refreshTranslationUi();
            } catch (_) { /* 保存に失敗したときは表示状態を変えない */ }
        });
        overlay.addEventListener('keydown', (event) => { if (event.key === 'Escape') close(); });
        (document.body || document.documentElement).appendChild(overlay);
        overlay.querySelector('.xtv-consent-cancel')?.focus();
    }

    document.addEventListener('xtv-translator-command', async () => {
        const command = document.documentElement.getAttribute('data-xtv-translator-command');
        document.documentElement.removeAttribute('data-xtv-translator-command');
        if (command === 'settings') {
            showConsentSettings();
            return;
        }
        if (command !== 'toggle') return;
        if (translationProvider !== 'google') {
            showConsentSettings();
            return;
        }
        if (!autoTranslateEnabled && !await requestTranslationConsent()) {
            publishTranslationState();
            return;
        }
        autoTranslateEnabled = !autoTranslateEnabled;
        try {
            await writeStoredSettings({ [autoTranslateKey]: autoTranslateEnabled });
        } catch (_) {
            autoTranslateEnabled = false;
        }
        refreshTranslationUi();
    });

    const chromeApi = getChrome();
    chromeApi?.storage?.onChanged?.addListener(async (changes, areaName) => {
        if (areaName !== 'local' || (!changes[translationConsentKey] && !changes[autoTranslateKey] && !changes[translationProviderKey])) return;
        const settings = await readStoredSettings();
        translationConsent = settings.consent;
        autoTranslateEnabled = settings.auto;
        translationProvider = settings.provider;
        if (!translationConsent) translationCache.clear();
        refreshTranslationUi();
    });

    async function init() {
        try {
            localStorage.removeItem(translationConsentKey);
            localStorage.removeItem(autoTranslateKey);
        } catch (_) { }
        const settings = await readStoredSettings();
        translationConsent = settings.consent;
        autoTranslateEnabled = settings.auto;
        translationProvider = settings.provider;
        if (!translationConsent) {
            try { await writeStoredSettings({ [autoTranslateKey]: false }); } catch (_) { }
        }
        if (translationProvider !== 'google' && autoTranslateEnabled) {
            autoTranslateEnabled = false;
            try { await writeStoredSettings({ [autoTranslateKey]: false }); } catch (_) { }
        }
        publishTranslationState();
        window.addEventListener?.('scroll', () => scheduleScan(), { passive: true, capture: true });
        window.addEventListener?.('resize', () => scheduleScan(), { passive: true });
        document.addEventListener('visibilitychange', () => scheduleScan());
        document.addEventListener('xtv-translator-language', refreshTranslationUi);
        scheduleScan(0);
        const observer = new MutationObserver(() => scheduleScan());
        observer.observe(document.body || document.documentElement, { childList: true, subtree: true, characterData: true });
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else void init();
})();
