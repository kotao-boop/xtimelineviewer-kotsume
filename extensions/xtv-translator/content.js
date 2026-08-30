// X Timeline Translator - コンテンツスクリプト
// ツイートの検知・言語判定・翻訳結果のインライン表示を行います

(function () {
    if (window._xtvTranslatorLoaded) return;
    window._xtvTranslatorLoaded = true;

    // 設定管理。X の localStorage は X 側スクリプトと共有されるため信頼しない。
    // 同意と自動翻訳の状態は、拡張機能だけが読める chrome.storage.local に保存する。
    const translationConsentKey = 'xtv_translation_external_consent_v1';
    const autoTranslateKey = 'xtv_auto_translate';
    let translationConsent = false;
    let autoTranslateEnabled = false;
    const translationCache = new Map();
    let pendingConsent = null;

    function readStoredSettings() {
        return new Promise((resolve) => {
            if (typeof chrome === 'undefined' || !chrome.storage?.local) {
                resolve({ consent: false, auto: false });
                return;
            }
            chrome.storage.local.get([translationConsentKey, autoTranslateKey], (result) => {
                if (chrome.runtime.lastError) {
                    resolve({ consent: false, auto: false });
                    return;
                }
                const consent = result[translationConsentKey] === true;
                resolve({ consent, auto: consent && result[autoTranslateKey] === true });
            });
        });
    }

    function writeStoredSettings(values) {
        return new Promise((resolve, reject) => {
            if (typeof chrome === 'undefined' || !chrome.storage?.local) {
                reject(new Error('拡張機能の安全な設定ストレージを利用できません。'));
                return;
            }
            chrome.storage.local.set(values, () => {
                if (chrome.runtime.lastError) reject(new Error(chrome.runtime.lastError.message));
                else resolve();
            });
        });
    }

    function refreshTranslationUi() {
        publishTranslationState();
        document.querySelectorAll('.xtv-translation-box, .xtv-manual-btn').forEach(el => el.remove());
        document.querySelectorAll('article[data-testid="tweet"]').forEach(tw => {
            tw.__xtvProcessing = false;
        });
        scanAndProcess();
    }

    // ON/OFF だけを DOM 属性へ公開する。投稿本文やアカウント情報は公開しない。
    // WebView2 側はこの属性を監視し、列ヘッダーの地球アイコンへ状態を反映する。
    function publishTranslationState() {
        document.documentElement.setAttribute(
            'data-xtv-translation-state',
            autoTranslateEnabled ? 'on' : 'off'
        );
    }

    // 外部送信の同意を、X側のUIと混同しない独立したモーダルで取得する。
    function requestTranslationConsent() {
        if (translationConsent) return Promise.resolve(true);
        if (pendingConsent) return pendingConsent;

        pendingConsent = new Promise((resolve) => {
            const overlay = document.createElement('div');
            overlay.className = 'xtv-consent-overlay';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');
            overlay.setAttribute('aria-labelledby', 'xtv-consent-title');
            overlay.innerHTML = `
                <div class="xtv-consent-card">
                    <div class="xtv-consent-brand">XTimelineViewer Kotsume Edition</div>
                    <h2 id="xtv-consent-title">翻訳時の外部送信について</h2>
                    <p>翻訳する投稿本文を、Google の翻訳用エンドポイントへ送信します。投稿に個人情報や秘密情報が含まれる場合は翻訳しないでください。</p>
                    <p class="xtv-consent-note">同意は右上の「同意設定」からいつでも取り消せます。この確認はXではなく、本アプリが表示しています。</p>
                    <div class="xtv-consent-links">
                        <a class="xtv-consent-link" href="https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md" target="_blank" rel="noopener noreferrer">アプリのプライバシーポリシー</a>
                        <a class="xtv-consent-link" href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Googleのプライバシーポリシー</a>
                    </div>
                    <div class="xtv-consent-actions">
                        <button type="button" class="xtv-consent-cancel">今回は使わない</button>
                        <button type="button" class="xtv-consent-accept">同意して翻訳する</button>
                    </div>
                </div>
            `;

            const finish = async (accepted) => {
                if (accepted) {
                    try {
                        await writeStoredSettings({
                            [translationConsentKey]: true,
                            [autoTranslateKey]: false
                        });
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
            overlay.querySelector('.xtv-consent-cancel').addEventListener('click', () => finish(false));
            overlay.querySelector('.xtv-consent-accept').addEventListener('click', () => finish(true));
            overlay.addEventListener('keydown', (event) => {
                if (event.key === 'Escape') finish(false);
            });

            (document.body || document.documentElement).appendChild(overlay);
            overlay.querySelector('.xtv-consent-accept').focus();
        });

        return pendingConsent;
    }

    // 日本語が含まれているか（ひらがな・カタカナの検知）
    function containsJapanese(text) {
        // ひらがな・カタカナが含まれている場合は日本語と判定
        return /[\u3040-\u309F\u30A0-\u30FF]/.test(text);
    }

    // 翻訳実行関数。外部通信は、同意を再確認するバックグラウンドだけに限定する。
    async function requestTranslation(text) {
        if (translationCache.has(text)) {
            return translationCache.get(text);
        }

        if (!translationConsent || typeof text !== 'string' || text.length > 10000) {
            return { text: null, lang: '', error: 'Translation is not permitted.' };
        }

        return new Promise((resolve) => {
            let completed = false;
            const finish = (result) => {
                if (completed) return;
                completed = true;
                resolve(result);
            };

            if (typeof chrome === 'undefined' || !chrome.runtime?.sendMessage) {
                finish({ text: null, lang: '', error: 'Translation service is unavailable.' });
                return;
            }

            chrome.runtime.sendMessage(
                { action: 'translate', text, targetLang: 'ja' },
                (response) => {
                    if (chrome.runtime.lastError || !response?.success) {
                        finish({ text: null, lang: '', error: response?.error || 'Translation failed.' });
                        return;
                    }
                    const result = {
                        text: response.translatedText,
                        lang: response.detectedLang
                    };
                    translationCache.set(text, result);
                    finish(result);
                }
            );

            setTimeout(() => finish({ text: null, lang: '', error: 'Translation timed out.' }), 10000);
        });
    }

    // ツイートへの翻訳要素の追加
    async function processTweet(tweetArticle) {
        if (tweetArticle.__xtvProcessing || tweetArticle.querySelector('.xtv-translation-box')) {
            return;
        }

        const tweetTextEl = tweetArticle.querySelector('[data-testid="tweetText"]');
        if (!tweetTextEl) return;

        const rawText = tweetTextEl.innerText.trim();
        if (!rawText || rawText.length < 2) return;

        // 日本語のみのツイートで自動翻訳が有効な場合はスキップ
        if (containsJapanese(rawText)) {
            return;
        }

        tweetArticle.__xtvProcessing = true;

        if (autoTranslateEnabled) {
            await applyTranslation(tweetArticle, tweetTextEl, rawText);
        } else {
            showTranslateButton(tweetArticle, tweetTextEl, rawText);
        }
    }

    // 翻訳実行とUI表示
    async function applyTranslation(tweetArticle, tweetTextEl, rawText) {
        let box = tweetArticle.querySelector('.xtv-translation-box');
        if (!box) {
            box = document.createElement('div');
            box.className = 'xtv-translation-box';
            box.innerHTML = `
                <div class="xtv-trans-header">
                    <span class="xtv-trans-badge">🌐 翻訳中...</span>
                </div>
            `;
            tweetTextEl.insertAdjacentElement('afterend', box);
        }

        const res = await requestTranslation(rawText);

        if (!res || !res.text || res.lang === 'ja') {
            // 日本語判定された場合は非表示にする
            if (box) box.remove();
            return;
        }

        const langMap = {
            'en': '英語', 'zh-CN': '中国語(簡体字)', 'zh-TW': '中国語(繁体字)',
            'ko': '韓国語', 'fr': 'フランス語', 'de': 'ドイツ語', 'es': 'スペイン語',
            'ru': 'ロシア語', 'it': 'イタリア語', 'pt': 'ポルトガル語', 'auto': '外国語'
        };
        const langName = langMap[res.lang] || res.lang || '外国語';

        box.innerHTML = `
            <div class="xtv-trans-header">
                <span class="xtv-trans-badge">🌐 ${langName}からの翻訳</span>
                <button type="button" class="xtv-trans-hide-btn" title="翻訳を非表示">✕</button>
            </div>
            <div class="xtv-trans-body">${escapeHtml(res.text)}</div>
        `;

        const hideBtn = box.querySelector('.xtv-trans-hide-btn');
        if (hideBtn) {
            hideBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                box.remove();
                showTranslateButton(tweetArticle, tweetTextEl, rawText);
            });
        }
    }

    // 手動翻訳ボタンの表示
    function showTranslateButton(tweetArticle, tweetTextEl, rawText) {
        if (tweetArticle.querySelector('.xtv-manual-btn') || tweetArticle.querySelector('.xtv-translation-box')) {
            return;
        }

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'xtv-manual-btn';
        btn.innerHTML = '🌐 翻訳を表示';
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            if (!await requestTranslationConsent()) return;
            btn.remove();
            await applyTranslation(tweetArticle, tweetTextEl, rawText);
        });

        tweetTextEl.insertAdjacentElement('afterend', btn);
    }

    function escapeHtml(str) {
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    // タイムラインの監視
    function scanAndProcess() {
        const tweets = document.querySelectorAll('article[data-testid="tweet"]');
        for (let i = 0; i < tweets.length; i++) {
            processTweet(tweets[i]);
        }
    }

    // 画面へ重ねる固定ボタンは廃止し、アプリの列ヘッダーから指令を受ける。
    // 指令は固定された2種類だけを受け付け、任意コードや文字列は実行しない。
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

    // 同じプロファイルの別タイムラインで切り替えた場合も、全列をすぐ同じ状態にする。
    chrome.storage?.onChanged?.addListener(async (changes, areaName) => {
        if (areaName !== 'local' ||
            (!changes[translationConsentKey] && !changes[autoTranslateKey])) return;
        const settings = await readStoredSettings();
        translationConsent = settings.consent;
        autoTranslateEnabled = settings.auto;
        refreshTranslationUi();
    });

    function showConsentSettings() {
        if (document.querySelector('.xtv-consent-overlay')) return;

        const overlay = document.createElement('div');
        overlay.className = 'xtv-consent-overlay';
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-modal', 'true');
        overlay.setAttribute('aria-labelledby', 'xtv-settings-title');
        overlay.innerHTML = `
            <div class="xtv-consent-card">
                <div class="xtv-consent-brand">XTimelineViewer Kotsume Edition</div>
                <h2 id="xtv-settings-title">翻訳データ送信の同意設定</h2>
                <p>現在の状態: <strong>${translationConsent ? '同意済み' : '未同意'}</strong></p>
                <p class="xtv-consent-note">同意を取り消すと自動翻訳もOFFになり、次回の翻訳時にもう一度説明を表示します。</p>
                <div class="xtv-consent-links">
                    <a class="xtv-consent-link" href="https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md" target="_blank" rel="noopener noreferrer">アプリのプライバシーポリシー</a>
                    <a class="xtv-consent-link" href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Googleのプライバシーポリシー</a>
                </div>
                <div class="xtv-consent-actions">
                    <button type="button" class="xtv-consent-cancel">閉じる</button>
                    ${translationConsent ? '<button type="button" class="xtv-consent-revoke">同意を取り消す</button>' : ''}
                </div>
            </div>
        `;

        const close = () => overlay.remove();
        overlay.addEventListener('click', (event) => {
            event.stopPropagation();
            if (event.target === overlay) close();
        });
        overlay.querySelector('.xtv-consent-cancel')?.addEventListener('click', close);
        overlay.querySelector('.xtv-consent-revoke')?.addEventListener('click', async () => {
            try {
                await writeStoredSettings({
                    [translationConsentKey]: false,
                    [autoTranslateKey]: false
                });
                translationConsent = false;
                autoTranslateEnabled = false;
                translationCache.clear();
                close();
                refreshTranslationUi();
            } catch (_) {
                // 保存に失敗した場合は同意済み表示を変えない。
            }
        });
        overlay.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') close();
        });
        (document.body || document.documentElement).appendChild(overlay);
        overlay.querySelector('.xtv-consent-cancel')?.focus();
    }

    // 初期化と監視開始
    async function init() {
        // 旧版がXのlocalStorageへ残した値は、信頼せず削除する。
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
        scanAndProcess();

        const observer = new MutationObserver(() => {
            scanAndProcess();
        });

        observer.observe(document.body || document.documentElement, {
            childList: true,
            subtree: true
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
