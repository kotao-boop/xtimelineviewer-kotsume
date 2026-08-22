// X Timeline Translator - コンテンツスクリプト
// ツイートの検知・言語判定・翻訳結果のインライン表示を行います

(function () {
    if (window._xtvTranslatorLoaded) return;
    window._xtvTranslatorLoaded = true;

    // 設定管理（既定値: 自動翻訳 ON）
    let autoTranslateEnabled = localStorage.getItem('xtv_auto_translate') !== 'false';
    const translationCache = new Map();

    // 日本語が含まれているか（ひらがな・カタカナの検知）
    function containsJapanese(text) {
        // ひらがな・カタカナが含まれている場合は日本語と判定
        return /[\u3040-\u309F\u30A0-\u30FF]/.test(text);
    }

    // 翻訳実行関数（バックグラウンド経由、失敗時は直接fetchを試みる）
    async function requestTranslation(text) {
        if (translationCache.has(text)) {
            return translationCache.get(text);
        }

        return new Promise((resolve) => {
            let handled = false;
            try {
                if (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.sendMessage) {
                    chrome.runtime.sendMessage(
                        { action: 'translate', text: text, targetLang: 'ja' },
                        (response) => {
                            if (!handled && response && response.success) {
                                handled = true;
                                const res = {
                                    text: response.translatedText,
                                    lang: response.detectedLang
                                };
                                translationCache.set(text, res);
                                resolve(res);
                            } else if (!handled) {
                                handled = true;
                                fallbackFetch(text).then(resolve);
                            }
                        }
                    );
                } else {
                    fallbackFetch(text).then(resolve);
                }
            } catch (e) {
                fallbackFetch(text).then(resolve);
            }

            // タイムアウトフォールバック
            setTimeout(() => {
                if (!handled) {
                    handled = true;
                    fallbackFetch(text).then(resolve);
                }
            }, 3000);
        });
    }

    // 直接fetchフォールバック
    async function fallbackFetch(text) {
        try {
            const url = `https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=ja&dt=t&q=${encodeURIComponent(text)}`;
            const res = await fetch(url);
            if (!res.ok) throw new Error('Fetch failed');
            const data = await res.json();
            const detectedLang = (data && data[2]) ? data[2] : '';
            let translatedText = '';
            if (data && data[0] && Array.isArray(data[0])) {
                translatedText = data[0].map(item => item[0] || '').join('');
            }
            const result = { text: translatedText, lang: detectedLang };
            translationCache.set(text, result);
            return result;
        } catch (err) {
            return { text: null, lang: '', error: err.message };
        }
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

    // 自動翻訳トグルボタンのヘッダー注入
    function injectHeaderToggle() {
        if (document.getElementById('xtv-toggle-container')) return;

        const container = document.createElement('div');
        container.id = 'xtv-toggle-container';
        container.className = 'xtv-toggle-container';
        container.innerHTML = `
            <button type="button" id="xtv-trans-toggle" class="xtv-header-toggle-btn ${autoTranslateEnabled ? 'active' : ''}" title="クリックで自動翻訳のON/OFFを切り替え">
                🌐 自動翻訳: ${autoTranslateEnabled ? 'ON' : 'OFF'}
            </button>
        `;

        document.body.appendChild(container);

        const toggleBtn = document.getElementById('xtv-trans-toggle');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', () => {
                autoTranslateEnabled = !autoTranslateEnabled;
                localStorage.setItem('xtv_auto_translate', autoTranslateEnabled ? 'true' : 'false');
                toggleBtn.textContent = `🌐 自動翻訳: ${autoTranslateEnabled ? 'ON' : 'OFF'}`;
                toggleBtn.classList.toggle('active', autoTranslateEnabled);

                // 全ツイートの再走査
                document.querySelectorAll('.xtv-translation-box, .xtv-manual-btn').forEach(el => el.remove());
                document.querySelectorAll('article[data-testid="tweet"]').forEach(tw => {
                    tw.__xtvProcessing = false;
                });
                scanAndProcess();
            });
        }
    }

    // 初期化と監視開始
    function init() {
        injectHeaderToggle();
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
