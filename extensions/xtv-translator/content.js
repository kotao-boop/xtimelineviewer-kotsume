// X Timeline Translator - コンテンツスクリプト
// 投稿の検知、同意確認、翻訳結果のインライン表示を行います。

(function () {
    if (window._xtvTranslatorLoaded) return;
    window._xtvTranslatorLoaded = true;

    const translationConsentKey = 'xtv_translation_external_consent_v1';
    const autoTranslateKey = 'xtv_auto_translate';
    const supportedTargetLanguages = Object.freeze(['ja', 'en']);
    const translationCacheLimit = 128;
    const maxTranslationChars = 10000;
    const scanDebounceMs = 120;
    const translationTimeoutMs = 10000;
    let translationConsent = false;
    let autoTranslateEnabled = false;
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
            settingsTitle: '翻訳データ送信の同意設定',
            status: value => value ? '同意済み' : '未同意',
            settingsNote: '同意を取り消すと自動翻訳もOFFになり、次回の翻訳時にもう一度説明を表示します。',
            close: '閉じる',
            revoke: '同意を取り消す',
            translating: '翻訳中...',
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
            settingsTitle: 'Translation data consent',
            status: value => value ? 'Consent given' : 'No consent',
            settingsNote: 'Withdrawing consent also turns off automatic translation. You will see this explanation again before the next translation.',
            close: 'Close',
            revoke: 'Withdraw consent',
            translating: 'Translating...',
            hide: 'Hide translation',
            show: 'Show translation',
            from: value => `Translated from ${value}`,
            unknownLanguage: 'another language',
            languages: { en: 'English', 'zh-CN': 'Simplified Chinese', 'zh-TW': 'Traditional Chinese', ko: 'Korean', fr: 'French', de: 'German', es: 'Spanish', ru: 'Russian', it: 'Italian', pt: 'Portuguese', auto: 'another language' }
        }
    };

    function getLocale() {
        const value = String(document.documentElement?.lang || window.navigator?.language || '').toLowerCase();
        return value.startsWith('en') ? 'en' : 'ja';
    }

    function getTargetLanguage() {
        const requested = getLocale();
        return supportedTargetLanguages.includes(requested) ? requested : 'ja';
    }

    function getChrome() {
        return typeof chrome === 'undefined' ? null : chrome;
    }

    // 同意と自動翻訳の状態は X の localStorage ではなく、拡張機能専用の chrome.storage.local に保存します。
    function readStoredSettings() {
        return new Promise((resolve) => {
            const api = getChrome();
            if (!api?.storage?.local) {
                resolve({ consent: false, auto: false });
                return;
            }
            api.storage.local.get([translationConsentKey, autoTranslateKey], (result) => {
                if (api.runtime?.lastError) {
                    resolve({ consent: false, auto: false });
                    return;
                }
                const consent = result?.[translationConsentKey] === true;
                resolve({ consent, auto: consent && result?.[autoTranslateKey] === true });
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

    function publishTranslationState() {
        document.documentElement.setAttribute('data-xtv-translation-state', autoTranslateEnabled ? 'on' : 'off');
    }

    function clearInjectedUi(tweet) {
        tweet.querySelectorAll?.('.xtv-translation-box, .xtv-manual-btn').forEach(el => el.remove());
    }

    function refreshTranslationUi() {
        publishTranslationState();
        document.querySelectorAll('.xtv-translation-box, .xtv-manual-btn').forEach(el => el.remove());
        document.querySelectorAll('article[data-testid="tweet"]').forEach(tweet => {
            const state = tweetStates.get(tweet);
            if (state) {
                state.generation++;
                state.processing = false;
                state.rawText = '';
                state.noTranslation = false;
            }
        });
        scheduleScan(0);
    }

    function requestTranslationConsent() {
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

    function containsJapanese(value) {
        return /[\u3040-\u309F\u30A0-\u30FF]/.test(value);
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
        if (!translationConsent || typeof text !== 'string' || text.length > maxTranslationChars) {
            return { text: null, lang: '', error: 'Translation is not permitted.' };
        }
        const api = getChrome();
        if (!api?.runtime?.sendMessage) return { text: null, lang: '', error: 'Translation service is unavailable.' };

        return new Promise((resolve) => {
            let completed = false;
            const finish = (result) => {
                if (completed) return;
                completed = true;
                resolve(result);
            };
            api.runtime.sendMessage({ action: 'translate', text, targetLang }, (response) => {
                if (api.runtime.lastError || !response?.success) {
                    finish({ text: null, lang: '', error: response?.error || 'Translation failed.' });
                    return;
                }
                const result = { text: response.translatedText, lang: response.detectedLang };
                cacheSet(targetLang, text, result);
                finish(result);
            });
            setTimeout(() => finish({ text: null, lang: '', error: 'Translation timed out.' }), translationTimeoutMs);
        });
    }

    function getState(tweet) {
        let state = tweetStates.get(tweet);
        if (!state) {
            state = { generation: 0, rawText: '', processing: false, noTranslation: false };
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
            // 一時的な失敗でMutationObserverが無限再試行しないよう、手動再試行へ戻します。
            showTranslateButton(tweet, textEl, rawText, generation);
            return;
        }
        if (result.lang === targetLang) {
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

    function showTranslateButton(tweet, textEl, rawText, generation) {
        if (!isCurrent(tweet, textEl, rawText, generation) || tweet.querySelector('.xtv-manual-btn, .xtv-translation-box')) return;
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'xtv-manual-btn';
        btn.innerHTML = `🌐 ${ui[getLocale()].show}`;
        btn.addEventListener('click', async (event) => {
            event.stopPropagation();
            if (!await requestTranslationConsent() || !isCurrent(tweet, textEl, rawText, generation)) return;
            btn.remove();
            const state = getState(tweet);
            state.processing = true;
            await applyTranslation(tweet, textEl, rawText, generation);
            if (isCurrent(tweet, textEl, rawText, generation)) state.processing = false;
        });
        textEl.insertAdjacentElement('afterend', btn);
    }

    async function processTweet(tweet) {
        if (!tweet?.matches?.('article[data-testid="tweet"]')) return;
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
            if (state.rawText !== rawText) {
                state.rawText = rawText;
                state.generation++;
                state.processing = false;
                state.noTranslation = false;
                clearInjectedUi(tweet);
            }
            return;
        }
        if (state.rawText !== rawText) {
            state.rawText = rawText;
            state.generation++;
            state.processing = false;
            state.noTranslation = false;
            clearInjectedUi(tweet);
        }
        const generation = state.generation;
        if (state.processing || tweet.querySelector('.xtv-translation-box, .xtv-manual-btn')) return;
        if (state.noTranslation) return;
        if (containsJapanese(rawText)) return;
        state.processing = true;
        if (autoTranslateEnabled && translationConsent) {
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
                <p class="xtv-consent-note">${text.settingsNote}</p>
                <div class="xtv-consent-links">
                    <a class="xtv-consent-link" href="https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md" target="_blank" rel="noopener noreferrer">${text.appPrivacy}</a>
                    <a class="xtv-consent-link" href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">${text.googlePrivacy}</a>
                </div>
                <div class="xtv-consent-actions">
                    <button type="button" class="xtv-consent-cancel">${text.close}</button>
                    ${translationConsent ? `<button type="button" class="xtv-consent-revoke">${text.revoke}</button>` : ''}
                </div>
            </div>`;
        const close = () => overlay.remove();
        overlay.addEventListener('click', (event) => {
            event.stopPropagation();
            if (event.target === overlay) close();
        });
        overlay.querySelector('.xtv-consent-cancel')?.addEventListener('click', close);
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
        if (areaName !== 'local' || (!changes[translationConsentKey] && !changes[autoTranslateKey])) return;
        const settings = await readStoredSettings();
        translationConsent = settings.consent;
        autoTranslateEnabled = settings.auto;
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
        if (!translationConsent) {
            try { await writeStoredSettings({ [autoTranslateKey]: false }); } catch (_) { }
        }
        publishTranslationState();
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
