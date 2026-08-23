// X Timeline Translator - バックグラウンド中継スクリプト
// Google翻訳の公開エンドポイントへのリクエストを処理します。
// 同意状態は X の localStorage ではなく、拡張機能専用の chrome.storage に保存します。

const translationConsentKey = 'xtv_translation_external_consent_v1';
const maxTranslationChars = 10000;

function getStoredConsent() {
    return new Promise((resolve) => {
        chrome.storage.local.get([translationConsentKey], (result) => {
            if (chrome.runtime.lastError) {
                resolve(false);
                return;
            }
            resolve(result[translationConsentKey] === true);
        });
    });
}

function isTrustedXSender(sender) {
    try {
        const url = new URL(sender?.url || sender?.tab?.url || '');
        return url.protocol === 'https:' &&
            (url.hostname === 'x.com' || url.hostname === 'www.x.com' ||
             url.hostname === 'twitter.com' || url.hostname === 'www.twitter.com');
    } catch (_) {
        return false;
    }
}

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request?.action === 'translate') {
        if (!isTrustedXSender(sender)) {
            sendResponse({ success: false, error: 'Untrusted translation request source.' });
            return false;
        }
        const text = typeof request.text === 'string' ? request.text.trim() : '';
        const targetLang = 'ja';
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000);

        getStoredConsent()
            .then((consented) => {
                if (!consented) throw new Error('Translation consent is not active.');
                if (!text || text.length > maxTranslationChars) {
                    throw new Error('Translation text is empty or too long.');
                }

                const body = new URLSearchParams({
                    client: 'gtx', sl: 'auto', tl: targetLang, dt: 't', q: text
                });
                return fetch('https://translate.googleapis.com/translate_a/single', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' },
                    body: body.toString(),
                    credentials: 'omit',
                    cache: 'no-store',
                    referrerPolicy: 'no-referrer',
                    signal: controller.signal
                });
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                const detectedLang = (data && data[2]) ? data[2] : '';
                let translatedText = '';
                if (data && data[0] && Array.isArray(data[0])) {
                    translatedText = data[0].map(item => item[0] || '').join('');
                }
                sendResponse({
                    success: true,
                    translatedText: translatedText,
                    detectedLang: detectedLang
                });
            })
            .catch(error => {
                sendResponse({
                    success: false,
                    error: error.name === 'AbortError'
                        ? 'Translation request timed out.'
                        : (error.message || 'Translation request failed')
                });
            })
            .finally(() => {
                clearTimeout(timeoutId);
            });

        return true; // 非同期レスポンスを待機
    }
});
