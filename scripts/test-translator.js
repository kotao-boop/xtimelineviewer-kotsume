/*
 * 翻訳拡張の依存なし Node テスト。
 * 実ブラウザを起動せず、background.js の chrome/fetch 境界をモックします。
 */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const backgroundSource = fs.readFileSync(
    path.join(__dirname, '..', 'extensions', 'xtv-translator', 'background.js'),
    'utf8'
);

function loadBackground({ consent = true, fetchImpl }) {
    let listener;
    const values = { xtv_translation_external_consent_v1: consent };
    const chrome = {
        runtime: {
            lastError: null,
            onMessage: { addListener(fn) { listener = fn; } }
        },
        storage: {
            local: {
                get(keys, callback) {
                    const result = {};
                    for (const key of keys) result[key] = values[key];
                    callback(result);
                }
            }
        }
    };
    const context = {
        chrome,
        fetch: fetchImpl,
        URL,
        URLSearchParams,
        AbortController,
        setTimeout,
        clearTimeout,
        console
    };
    vm.runInNewContext(backgroundSource, context, { filename: 'background.js' });
    assert.equal(typeof listener, 'function', 'background listener is registered');
    return listener;
}

function invoke(listener, request, sender = { url: 'https://x.com/home' }) {
    return new Promise((resolve) => {
        const result = listener(request, sender, resolve);
        assert.equal(result, request.action === 'translate' ? true : false);
    });
}

// content.js 用の小さな DOM。ブラウザー実装全体ではなく、拡張が実際に使う
// querySelector / MutationObserver / イベントだけを再現し、外部依存なしで非同期境界を検証する。
class MockElement {
    constructor(tagName, root = false) {
        this.tagName = tagName.toUpperCase();
        this.children = [];
        this.parentNode = null;
        this.attributes = new Map();
        this.listeners = new Map();
        this._text = '';
        this._rawHtml = '';
        this._root = root;
        this.className = '';
    }

    get isConnected() {
        if (this._root) return true;
        return this.parentNode ? this.parentNode.isConnected : false;
    }

    set innerText(value) { this._text = String(value); }
    get innerText() {
        if (this._text) return this._text;
        return this.children.map(child => child.innerText).join('');
    }

    set textContent(value) { this.innerText = value; }
    get textContent() { return this.innerText; }

    set innerHTML(value) {
        this._rawHtml = String(value);
        this._text = '';
        this.children = [];
        const stack = [this];
        const tokens = String(value).match(/<[^>]+>|[^<]+/g) || [];
        for (const token of tokens) {
            if (token.startsWith('</')) {
                if (stack.length > 1) stack.pop();
                continue;
            }
            if (!token.startsWith('<')) {
                stack[stack.length - 1]._text += token;
                continue;
            }
            const opening = token.match(/^<([a-z][\w-]*)([^>]*)>/i);
            if (!opening || token.startsWith('<!')) continue;
            const child = new MockElement(opening[1]);
            const attrs = opening[2];
            for (const match of attrs.matchAll(/([\w:-]+)\s*=\s*["']([^"']*)["']/g)) {
                child.setAttribute(match[1], match[2]);
            }
            stack[stack.length - 1].appendChild(child);
            if (!token.endsWith('/>') && !['br', 'img', 'input', 'meta', 'link'].includes(child.tagName.toLowerCase())) {
                stack.push(child);
            }
        }
    }
    get innerHTML() { return this._rawHtml; }

    appendChild(child) {
        if (child.parentNode) child.parentNode.removeChild(child);
        child.parentNode = this;
        this.children.push(child);
        return child;
    }

    removeChild(child) {
        const index = this.children.indexOf(child);
        if (index >= 0) {
            this.children.splice(index, 1);
            child.parentNode = null;
        }
        return child;
    }

    remove() { this.parentNode?.removeChild(this); }
    setAttribute(name, value) {
        this.attributes.set(name, String(value));
        if (name === 'class') this.className = String(value);
    }
    getAttribute(name) { return this.attributes.get(name) ?? null; }
    focus() {}

    addEventListener(type, handler) {
        const list = this.listeners.get(type) || [];
        list.push(handler);
        this.listeners.set(type, list);
    }
    dispatchEvent(event) {
        const actual = event || {};
        actual.target = actual.target || this;
        actual.currentTarget = this;
        actual.stopPropagation = actual.stopPropagation || (() => {});
        for (const handler of this.listeners.get(actual.type) || []) handler(actual);
    }

    insertAdjacentElement(position, element) {
        assert.equal(position, 'afterend');
        assert.ok(this.parentNode, 'insertAdjacentElement requires a parent');
        const siblings = this.parentNode.children;
        const index = siblings.indexOf(this);
        if (element.parentNode) element.parentNode.removeChild(element);
        element.parentNode = this.parentNode;
        siblings.splice(index + 1, 0, element);
        return element;
    }

    matches(selector) { return matchesSelector(this, selector); }
    querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
    querySelectorAll(selector) {
        const result = [];
        const visit = (node) => {
            for (const child of node.children) {
                if (matchesSelector(child, selector)) result.push(child);
                visit(child);
            }
        };
        visit(this);
        return result;
    }
}

function matchesSelector(element, selector) {
    return selector.split(',').some((part) => {
        const value = part.trim();
        const classMatches = [...value.matchAll(/\.([\w-]+)/g)].every(match =>
            element.className.split(/\s+/).includes(match[1]));
        if (!classMatches) return false;
        const attribute = value.match(/\[([\w:-]+)=["']?([^\]"']+)["']?\]/);
        if (attribute && element.getAttribute(attribute[1]) !== attribute[2]) return false;
        const tag = value.match(/^([a-z][\w-]*)/i);
        return !tag || element.tagName.toLowerCase() === tag[1].toLowerCase();
    });
}

class MockDocument extends MockElement {
    constructor() {
        super('#document', true);
        this.documentElement = new MockElement('html', true);
        this.body = new MockElement('body');
        this.documentElement.appendChild(this.body);
        this.appendChild(this.documentElement);
        this.readyState = 'complete';
    }
    createElement(tagName) { return new MockElement(tagName); }
}

function makeTweet(document, text) {
    const article = document.createElement('article');
    article.setAttribute('data-testid', 'tweet');
    const textElement = document.createElement('div');
    textElement.setAttribute('data-testid', 'tweetText');
    textElement.innerText = text;
    article.appendChild(textElement);
    document.body.appendChild(article);
    return { article, textElement };
}

async function loadContent({ auto = false, language = 'ja-JP', sendMessage }) {
    const source = fs.readFileSync(
        path.join(__dirname, '..', 'extensions', 'xtv-translator', 'content.js'),
        'utf8'
    );
    const document = new MockDocument();
    document.documentElement.lang = language;
    const observers = [];
    const values = { xtv_translation_external_consent_v1: true, xtv_auto_translate: auto };
    const chrome = {
        runtime: { lastError: null, sendMessage },
        storage: {
            local: {
                get(keys, callback) { callback(Object.fromEntries(keys.map(key => [key, values[key]]))); },
                set(changes, callback) { Object.assign(values, changes); callback?.(); }
            },
            onChanged: { addListener() {} }
        }
    };
    class MockMutationObserver {
        constructor(callback) { this.callback = callback; observers.push(this); }
        observe() {}
        disconnect() {}
        trigger() { this.callback([]); }
    }
    const window = { navigator: { language }, _xtvTranslatorLoaded: false };
    window.window = window;
    // テストでは翻訳応答を必ず返すため、10 秒の製品タイムアウトが Node プロセスを保持しないよう短絡する。
    const contentSetTimeout = (callback, delay) =>
        setTimeout(delay >= 10000 ? () => {} : callback, delay >= 10000 ? 0 : delay);
    const context = {
        window,
        document,
        chrome,
        MutationObserver: MockMutationObserver,
        localStorage: { removeItem() {} },
        setTimeout: contentSetTimeout,
        clearTimeout,
        console
    };
    vm.runInNewContext(source, context, { filename: 'content.js' });
    await waitFor(5);
    assert.equal(observers.length, 1, 'content script installs a MutationObserver');
    return { document, chrome, observer: observers[0] };
}

function waitFor(milliseconds = 0) {
    return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function testStaleTranslationIsDiscarded() {
    const pending = [];
    const env = await loadContent({
        auto: true,
        sendMessage(request, callback) { pending.push({ request, callback }); }
    });
    const tweet = makeTweet(env.document, 'text A');
    env.observer.trigger();
    await waitFor(140);
    assert.equal(pending.length, 1);
    assert.equal(pending[0].request.text, 'text A');

    // X が同じ article/text 要素を再利用して本文だけ B に差し替えた状態。
    tweet.textElement.innerText = 'text B';
    env.observer.trigger();
    await waitFor(140);
    assert.equal(pending.length, 2);
    assert.equal(pending[1].request.text, 'text B');

    pending[0].callback({ success: true, translatedText: '翻訳A', detectedLang: 'en' });
    await waitFor(0);
    assert.equal(env.document.querySelector('.xtv-trans-body'), null, '古い A の結果は表示されない');

    pending[1].callback({ success: true, translatedText: '翻訳B', detectedLang: 'en' });
    await waitFor(0);
    const body = env.document.querySelector('.xtv-trans-body');
    assert.ok(body);
    assert.equal(body.innerText, '翻訳B');
}

async function testFailureCanBeRetriedManually() {
    const responses = [
        { success: false, error: 'temporary failure' },
        { success: true, translatedText: '再試行成功', detectedLang: 'en' }
    ];
    let calls = 0;
    const env = await loadContent({
        auto: false,
        sendMessage(_request, callback) {
            calls++;
            callback(responses.shift());
        }
    });
    const tweet = makeTweet(env.document, 'retry me');
    env.observer.trigger();
    await waitFor(140);
    let button = tweet.article.querySelector('.xtv-manual-btn');
    assert.ok(button, '自動翻訳OFFでは手動ボタンが表示される');

    button.dispatchEvent({ type: 'click' });
    await waitFor(0);
    button = tweet.article.querySelector('.xtv-manual-btn');
    assert.ok(button, '失敗後は手動再試行ボタンへ戻る');
    assert.equal(calls, 1);

    button.dispatchEvent({ type: 'click' });
    await waitFor(0);
    assert.equal(calls, 2);
    assert.equal(tweet.article.querySelector('.xtv-trans-body')?.innerText, '再試行成功');
}

async function testCacheSeparatesLanguageAndIsBounded() {
    const calls = [];
    const env = await loadContent({
        auto: true,
        language: 'ja-JP',
        sendMessage(request, callback) {
            calls.push(request);
            callback({
                success: true,
                translatedText: `out:${request.text}:${request.targetLang}`,
                detectedLang: request.targetLang === 'ja' ? 'en' : 'ja'
            });
        }
    });
    makeTweet(env.document, 'same text');
    env.observer.trigger();
    await waitFor(140);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].targetLang, 'ja');

    // 同じ本文・同じ対象言語はキャッシュから返る。
    makeTweet(env.document, 'same text');
    env.observer.trigger();
    await waitFor(140);
    assert.equal(calls.length, 1);

    // 対象言語が変わると同じ本文でも別キャッシュエントリになる。
    env.document.documentElement.lang = 'en-US';
    makeTweet(env.document, 'same text');
    env.observer.trigger();
    await waitFor(140);
    assert.equal(calls.length, 2);
    assert.equal(calls[1].targetLang, 'en');

    // 128 件を超えると最古の本文が追い出され、再取得される。
    const beforeCapacity = calls.length;
    for (let index = 0; index < 129; index++) makeTweet(env.document, `capacity-${index}`);
    env.observer.trigger();
    await waitFor(160);
    const afterCapacity = calls.length;
    assert.equal(afterCapacity - beforeCapacity, 129);
    makeTweet(env.document, 'capacity-0');
    env.observer.trigger();
    await waitFor(140);
    assert.equal(calls.length, afterCapacity + 1, 'キャッシュ上限を超えた最古の項目は再取得される');
}

async function run() {
    let calls = 0;
    const untrusted = loadBackground({
        fetchImpl: async () => { calls++; throw new Error('must not fetch'); }
    });
    const untrustedResult = await invoke(untrusted, { action: 'translate', text: 'hello', targetLang: 'en' }, { url: 'https://evil.example/' });
    assert.equal(untrustedResult.success, false);
    assert.equal(calls, 0, 'untrusted pages cannot send translation requests');

    const noConsent = loadBackground({
        consent: false,
        fetchImpl: async () => { calls++; throw new Error('must not fetch'); }
    });
    const noConsentResult = await invoke(noConsent, { action: 'translate', text: 'hello', targetLang: 'en' });
    assert.equal(noConsentResult.success, false);
    assert.equal(calls, 0, 'background re-checks consent before fetch');

    const requests = [];
    const allowlist = loadBackground({
        fetchImpl: async (_url, options) => {
            requests.push(new URLSearchParams(options.body));
            return { ok: true, status: 200, json: async () => [['こんにちは', 'hello'], null, 'en'] };
        }
    });
    const allowlistResult = await invoke(allowlist, { action: 'translate', text: 'hello', targetLang: 'fr' });
    assert.equal(allowlistResult.success, true);
    assert.equal(requests[0].get('tl'), 'ja', 'unsupported target language falls back to ja');
    assert.equal(requests[0].get('q'), 'hello');

    let retryCalls = 0;
    const retry = loadBackground({
        fetchImpl: async () => {
            retryCalls++;
            if (retryCalls === 1) throw new Error('temporary network failure');
            return { ok: true, status: 200, json: async () => [['こんにちは', 'hello'], null, 'en'] };
        }
    });
    const retryResult = await invoke(retry, { action: 'translate', text: 'hello', targetLang: 'en' });
    assert.equal(retryResult.success, true, 'temporary network failures are retried');
    assert.equal(retryCalls, 2);

    let statusCalls = 0;
    const statusRetry = loadBackground({
        fetchImpl: async () => {
            statusCalls++;
            if (statusCalls === 1) return { ok: false, status: 503, json: async () => ({}) };
            return { ok: true, status: 200, json: async () => [['こんにちは', 'hello'], null, 'en'] };
        }
    });
    const statusResult = await invoke(statusRetry, { action: 'translate', text: 'hello', targetLang: 'en' });
    assert.equal(statusResult.success, true, 'retryable HTTP failures are retried');
    assert.equal(statusCalls, 2);

    let finalCalls = 0;
    const finalFailure = loadBackground({
        fetchImpl: async () => {
            finalCalls++;
            return { ok: false, status: 400, json: async () => ({}) };
        }
    });
    const finalResult = await invoke(finalFailure, { action: 'translate', text: 'hello', targetLang: 'ja' });
    assert.equal(finalResult.success, false);
    assert.match(finalResult.error, /400/);
    assert.equal(finalCalls, 1, 'non-retryable HTTP failures are not retried');

    await testStaleTranslationIsDiscarded();
    await testFailureCanBeRetriedManually();
    await testCacheSeparatesLanguageAndIsBounded();

    const source = fs.readFileSync(path.join(__dirname, '..', 'extensions', 'xtv-translator', 'content.js'), 'utf8');
    assert.match(source, /translationCacheLimit\s*=\s*128/);
    assert.match(source, /cacheKey\(targetLang, text\)/);
    assert.match(source, /scheduleScan\(\)/);
    assert.match(source, /state\.generation/);
    assert.match(source, /targetLang\}/);
    console.log('translator tests passed');
}

run().catch((error) => {
    console.error(error.stack || error);
    process.exitCode = 1;
});
