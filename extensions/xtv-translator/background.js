// X Timeline Translator - バックグラウンド中継スクリプト
// Google翻訳の公開エンドポイントへのリクエストを処理します

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request.action === 'translate') {
        const text = request.text;
        const targetLang = request.targetLang || 'ja';
        const url = `https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=${encodeURIComponent(targetLang)}&dt=t&q=${encodeURIComponent(text)}`;

        fetch(url)
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
                    error: error.message || 'Translation request failed'
                });
            });

        return true; // 非同期レスポンスを待機
    }
});
