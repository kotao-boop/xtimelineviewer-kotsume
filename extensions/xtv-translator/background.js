// X Timeline Translator - バックグラウンド中継スクリプト
// Google翻訳の公開エンドポイントへのリクエストを処理します。
// 同意状態は X の localStorage ではなく、拡張機能専用の chrome.storage に保存します。

const translationConsentKey = 'xtv_translation_external_consent_v1';
// 翻訳先の選択。現時点では Google と無効化を扱い、将来のローカル翻訳先を
// 同じ設定へ追加できるように値を固定する。
const translationProviderKey = 'xtv_translation_provider_v1';
const defaultTranslationProvider = 'google';
const supportedTranslationProviders = Object.freeze(['google', 'disabled']);
const maxTranslationChars = 10000;
const supportedTargetLanguages = Object.freeze(['ja', 'en']);
const defaultTargetLanguage = 'ja';
const maxTranslationAttempts = 2;
const retryDelayMs = 1500;

function normalizeTargetLanguage(value) {
    return supportedTargetLanguages.includes(value) ? value : defaultTargetLanguage;
}

function normalizeTranslationProvider(value) {
    return supportedTranslationProviders.includes(value) ? value : defaultTranslationProvider;
}

function shouldRetryTranslation(error, response) {
    if (response) return response.status >= 500;
    return error?.name !== 'AbortError';
}

function wait(milliseconds) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}

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

function getStoredTranslationProvider() {
    return new Promise((resolve) => {
        chrome.storage.local.get([translationProviderKey], (result) => {
            if (chrome.runtime.lastError) {
                resolve(defaultTranslationProvider);
                return;
            }
            resolve(normalizeTranslationProvider(result?.[translationProviderKey]));
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

// One worker per WebView2 profile: all columns share pacing, in-flight work and cache.
const requestIntervalMs = 1500;
const resultCache = new Map();
const inFlight = new Map();
let nextRequestAt = 0;
let blockedUntil = 0;
let cooldownMs = 30000;
let cacheChars = 0;
const cooldownReady = new Promise(resolve => {
    chrome.storage.local.get(['xtv_translation_blocked_until', 'xtv_translation_cooldown_ms'], values => {
        if (!chrome.runtime.lastError && Number.isFinite(values?.xtv_translation_blocked_until)) {
            blockedUntil = values.xtv_translation_blocked_until;
        }
        if (Number.isFinite(values?.xtv_translation_cooldown_ms)) {
            cooldownMs = Math.max(30000, Math.min(values.xtv_translation_cooldown_ms, 15 * 60 * 1000));
        }
        resolve();
    });
});

function failure(code, error, retryAt = 0) {
    return { success: false, code, error, retryAt };
}

function remember(key, value) {
    const size = key.length + value.translatedText.length;
    if (size > 1000000) return;
    resultCache.set(key, { value, size });
    cacheChars += size;
    while (resultCache.size > 256 || cacheChars > 1000000) {
        const oldest = resultCache.keys().next().value;
        cacheChars -= resultCache.get(oldest).size;
        resultCache.delete(oldest);
    }
}

async function translate(text, targetLang) {
    if (await getStoredTranslationProvider() !== 'google') {
        return failure('provider_disabled', 'Google translation is disabled.');
    }
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 10000);
    try {
        const body = new URLSearchParams({ client: 'gtx', sl: 'auto', tl: targetLang, dt: 't', q: text });
        for (let attempt = 1; attempt <= maxTranslationAttempts; attempt++) {
            if (!await getStoredConsent()) return failure('consent', 'Translation consent is not active.');
            let response;
            try {
                response = await fetch('https://translate.googleapis.com/translate_a/single', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' },
                    body: body.toString(), credentials: 'omit', cache: 'no-store',
                    referrerPolicy: 'no-referrer', signal: controller.signal
                });
            } catch (error) {
                if (attempt === maxTranslationAttempts || error.name === 'AbortError') throw error;
                await wait(retryDelayMs * attempt);
                continue;
            }
            if (response.status === 429) {
                const header = response.headers?.get('Retry-After');
                const seconds = header && /^\d+(\.\d+)?$/.test(header.trim()) ? Number(header) : NaN;
                const serverDeadline = Number.isFinite(seconds) ? Date.now() + seconds * 1000 : Date.parse(header || '');
                blockedUntil = Math.max(Date.now() + cooldownMs, Number.isFinite(serverDeadline) ? serverDeadline : 0);
                cooldownMs = Math.min(cooldownMs * 2, 15 * 60 * 1000);
                // Only timing metadata is persisted; post text and translations remain in memory.
                await new Promise(resolve => chrome.storage.local.set(
                    { xtv_translation_blocked_until: blockedUntil, xtv_translation_cooldown_ms: cooldownMs },
                    () => { void chrome.runtime.lastError; resolve(); }));
                return failure('rate_limited', 'Translation is temporarily rate limited.', blockedUntil);
            }
            if (!response.ok) {
                if (shouldRetryTranslation(null, response) && attempt < maxTranslationAttempts) {
                    await wait(retryDelayMs * attempt);
                    continue;
                }
                return failure('http', `HTTP error! status: ${response.status}`);
            }
            const data = await response.json();
            const translatedText = Array.isArray(data?.[0])
                ? data[0].map(part => Array.isArray(part) && typeof part[0] === 'string' ? part[0] : '').join('') : '';
            if (!translatedText) return failure('response', 'Translation response is empty.');
            if (!await getStoredConsent()) return failure('consent', 'Translation consent is not active.');
            cooldownMs = 30000;
            chrome.storage.local.set({ xtv_translation_blocked_until: 0, xtv_translation_cooldown_ms: cooldownMs },
                () => { void chrome.runtime.lastError; });
            return { success: true, translatedText, detectedLang: typeof data[2] === 'string' ? data[2] : '' };
        }
        return failure('network', 'Translation request failed.');
    } catch (error) {
        return failure(error.name === 'AbortError' ? 'timeout' : 'network', 'Translation request failed.');
    } finally {
        clearTimeout(timeoutId);
        nextRequestAt = Date.now() + requestIntervalMs;
    }
}

async function dispatchTranslation(request) {
    await cooldownReady;
    if (await getStoredTranslationProvider() !== 'google') {
        return failure('provider_disabled', 'Google translation is disabled.');
    }
    if (!await getStoredConsent()) return failure('consent', 'Translation consent is not active.');
    const text = typeof request.text === 'string' ? request.text.trim() : '';
    if (!text || text.length > maxTranslationChars) return failure('input', 'Translation text is empty or too long.');
    const targetLang = normalizeTargetLanguage(request.targetLang);
    const key = `${targetLang}\u0000${text}`;
    if (resultCache.has(key)) {
        const entry = resultCache.get(key);
        resultCache.delete(key);
        resultCache.set(key, entry);
        return entry.value;
    }
    if (inFlight.has(key)) return inFlight.get(key);
    if (Date.now() < blockedUntil) return failure('rate_limited', 'Translation is temporarily rate limited.', blockedUntil);
    // Keep waiting posts in their pages, where visibility and consent can be rechecked.
    if (inFlight.size || Date.now() < nextRequestAt) {
        return failure('busy', 'Waiting for translation.', Math.max(nextRequestAt, Date.now() + requestIntervalMs));
    }
    const task = translate(text, targetLang);
    inFlight.set(key, task);
    try {
        const result = await task;
        if (result.success) remember(key, result);
        return result;
    } finally { inFlight.delete(key); }
}

chrome.storage.onChanged?.addListener((changes, area) => {
    if (area === 'local' && (changes[translationConsentKey] || changes[translationProviderKey])) {
        resultCache.clear();
        cacheChars = 0;
    }
});

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request?.action !== 'translate') return false;
    if (!isTrustedXSender(sender)) {
        sendResponse(failure('source', 'Untrusted translation request source.'));
        return false;
    }
    dispatchTranslation(request).then(sendResponse).catch(() => sendResponse(failure('network', 'Translation unavailable.')));
    return true;
});
