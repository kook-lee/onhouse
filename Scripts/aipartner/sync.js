const fs = require('fs');
const path = require('path');
const vm = require('vm');

async function main() {
    const args = process.argv.slice(2);
    let memberId = args[0] || '';
    let memberPw = args[1] || '';

    if (!memberId || !memberPw) {
        console.log(JSON.stringify({
            success: false,
            message: '아이디와 비밀번호가 제공되지 않았습니다.',
            articleNumbers: [],
            logs: ['아이디 혹은 비밀번호가 비어 있습니다.']
        }));
        process.exit(1);
    }

    const logs = [];
    function log(msg) {
        const time = new Date().toTimeString().split(' ')[0];
        logs.push(`[${time}] ${msg}`);
    }

    try {
        log(`이실장(aipartner.com) SSO 로그인 시작 (계정: ${memberId})`);

        let cookieJar = {};
        function saveCookies(res) {
            const setCookies = res.headers.getSetCookie ? res.headers.getSetCookie() : [];
            const raw = res.headers.get('set-cookie');
            const list = setCookies.length ? setCookies : (raw ? raw.split(/,\s*(?=[a-zA-Z0-9_-]+=)/) : []);
            for (const c of list) {
                const parts = c.split(';')[0].split('=');
                if (parts.length >= 2) cookieJar[parts[0].trim()] = parts.slice(1).join('=').trim();
            }
        }
        function getCookieHeader() {
            return Object.entries(cookieJar).map(([k, v]) => `${k}=${v}`).join('; ');
        }

        // 1. 게이트웨이 접속 및 CSRF 획득
        const loginPageRes = await fetch('https://www.aipartner.com/integrated/login?serviceCode=1000', {
            headers: { 'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36' }
        });
        saveCookies(loginPageRes);
        const loginHtml = await loginPageRes.text();
        let csrfToken = (loginHtml.match(/name="csrf-token"\s+content="([^"]+)"/) || [])[1] || '';
        log('이실장 게이트웨이 접속 성공');

        // 2. SSO 공개키 및 타임스탬프 획득
        const pkRes = await fetch('https://sso.aipartner.com/openapi/authentication/publickey/get', {
            headers: { 'Cookie': getCookieHeader(), 'Referer': 'https://www.aipartner.com/integrated/login?serviceCode=1000' }
        });
        saveCookies(pkRes);
        const pkJson = await pkRes.json();
        if (pkJson.resultCode !== '000000') {
            throw new Error(`SSO 보안 공개키 수신 실패: [${pkJson.resultCode}] ${pkJson.resultMessage}`);
        }
        log('이실장 SSO 암호화 키 협상 완료');

        // 3. Penta Security IssacWeb 암호화
        const context = {
            window: {},
            document: { location: { href: 'https://www.aipartner.com/integrated/login?serviceCode=1000' } },
            navigator: { userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)', language: 'ko-KR' },
            console, setTimeout, clearTimeout
        };
        context.window = context;
        vm.createContext(context);

        const jsDir = path.join(__dirname, 'js');
        const files = [
            'forge.js', 'jsbn.js', 'util.js', 'sha1.js', 'sha256.js', 'sha512.js',
            'asn1.js', 'cipher.js', 'cipherModes.js', 'seed.js', 'aes.js',
            'prng.js', 'random.js', 'rsa.js', 'pkcs1.js',
            'webcrypto.js', 'webcrypto_msg.js', 'webcrypto_e2e.js'
        ];
        for (const f of files) {
            try { vm.runInContext(fs.readFileSync(path.join(jsDir, f), 'utf8'), context); } catch (e) {}
        }

        let message = encodeURIComponent('id') + "=" + encodeURIComponent(memberId) +
                      "&" + encodeURIComponent('pw') + "=" + encodeURIComponent(memberPw) +
                      "&" + encodeURIComponent('timeStamp') + "=" + encodeURIComponent(pkJson.resultData.timeStamp);

        const issacwebData = await new Promise((resolve, reject) => {
            const req = context.webcrypto.e2e.hybridEncrypt(
                undefined, message, 'UTF-8', 'SEED', pkJson.resultData.publicKey, 'RSAES-OAEP', 'RSA-SHA1'
            );
            req.onerror = reject;
            req.oncomplete = resolve;
        });

        // 4. 로그인 정보 임시 보관
        const saveFormData = new URLSearchParams();
        saveFormData.append('requestPage', 'https://www.aipartner.com/home');
        saveFormData.append('serviceCode', '1000');
        saveFormData.append('formData[member-id]', memberId);
        saveFormData.append('formData[member-pw]', memberPw);
        saveFormData.append('formData[agentId]', '100');
        saveFormData.append('formData[serviceCode]', '1000');
        saveFormData.append('formData[loginCode]', '1');
        saveFormData.append('formData[requestPage]', 'https://www.aipartner.com/home');
        if (csrfToken) saveFormData.append('_token', csrfToken);

        await fetch('https://www.aipartner.com/api/web/integrated/login-info-save', {
            method: 'POST',
            headers: { 'X-CSRF-TOKEN': csrfToken, 'Cookie': getCookieHeader(), 'Origin': 'https://www.aipartner.com' },
            body: saveFormData
        });

        // 5. SSO 인증 실행
        const loginProcParams = new URLSearchParams();
        loginProcParams.append('agentId', '100');
        loginProcParams.append('loginCode', '1');
        loginProcParams.append('issacwebData', issacwebData);
        loginProcParams.append('serviceCode', '1000');

        const procRes = await fetch('https://sso.aipartner.com/authentication/issacweb/loginProcess', {
            method: 'POST',
            headers: { 'Cookie': getCookieHeader(), 'Origin': 'https://www.aipartner.com' },
            body: loginProcParams
        });
        saveCookies(procRes);
        const procText = await procRes.text();
        const resultCode = (procText.match(/id="resultCode"[^>]*value="([^"]*)"/) || [])[1] || '';
        const resultMsg = (procText.match(/id="resultMessage"[^>]*value="([^"]*)"/) || [])[1] || '';
        const secureToken = (procText.match(/id="secureToken"[^>]*value="([^"]*)"/) || [])[1] || '';
        const secureSessionId = (procText.match(/id="secureSessionId"[^>]*value="([^"]*)"/) || [])[1] || '';

        if (resultCode !== '000000') {
            log(`❌ 이실장 로그인 실패: [${resultCode}] ${resultMsg}`);
            console.log(JSON.stringify({
                success: false,
                message: resultMsg || '이실장 로그인에 실패했습니다. 아이디 또는 비밀번호를 확인해주세요.',
                articleNumbers: [],
                logs
            }));
            return;
        }

        log('🎉 이실장 SSO 본인인증 성공! 보안 토큰 발급 완료');

        // 6. checkAuth
        const finishParams = new URLSearchParams();
        finishParams.append('resultCode', resultCode);
        finishParams.append('resultMessage', resultMsg);
        finishParams.append('secureToken', secureToken);
        finishParams.append('secureSessionId', secureSessionId);
        finishParams.append('userId', memberId);
        finishParams.append('agentId', '100');

        await fetch('https://www.aipartner.com/api/web/sso/checkAuth', {
            method: 'POST',
            headers: { 'Cookie': getCookieHeader(), 'Origin': 'https://sso.aipartner.com' },
            body: finishParams
        });

        // 7. saveToken
        const tokenParams = new URLSearchParams();
        tokenParams.append('agentId', '100');
        tokenParams.append('resultCode', '000000');
        tokenParams.append('secureSessionId', secureSessionId);

        await fetch('https://sso.aipartner.com/token/saveToken.html', {
            method: 'POST',
            headers: { 'Cookie': getCookieHeader() },
            body: tokenParams
        });

        // 8. agentProc (세션 최종 확정)
        await fetch('https://www.aipartner.com/api/web/sso/agentProc', {
            method: 'POST',
            headers: { 'Cookie': getCookieHeader() }
        });

        log('이실장 포털 정식 세션 쿠키 수령 완료');

        // 9. 광고 매물 목록 다중 페이지 조회
        const allArticleNos = new Set();
        for (let page = 1; page <= 10; page++) {
            const url = `https://www.aipartner.com/offerings/ad_list?adName=ad&page=${page}`;
            const res = await fetch(url, { headers: { 'Cookie': getCookieHeader() } });
            const html = await res.text();
            
            const matches = (html.match(/<div class="numberN"[^>]*>.*?([0-9]{9,11}).*?<\/div>/gs) || [])
                .map(x => (x.match(/([0-9]{9,11})/) || [])[1])
                .filter(Boolean);

            // 보조 정규식 (data-seq 등)
            const seqMatches = (html.match(/data-seq=["']([0-9]{9,11})["']/g) || [])
                .map(x => (x.match(/([0-9]{9,11})/) || [])[1])
                .filter(Boolean);

            const pageArticles = new Set([...matches, ...seqMatches]);
            let newInPage = 0;
            for (const a of pageArticles) {
                if (!allArticleNos.has(a)) {
                    allArticleNos.add(a);
                    newInPage++;
                }
            }

            if (pageArticles.size > 0) {
                log(`📄 광고 매물 ${page}페이지 조회: ${pageArticles.size}건 확인 (누적 ${allArticleNos.size}건)`);
            }

            if (pageArticles.size === 0) break;
        }

        const articleList = Array.from(allArticleNos);
        log(`✨ 이실장 전체 광고 매물 수집 완료: 총 ${articleList.length}건`);

        console.log(JSON.stringify({
            success: true,
            message: `이실장 연동 성공! 총 ${articleList.length}건의 광고 매물 식별 완료`,
            articleNumbers: articleList,
            logs
        }));
    } catch (err) {
        log(`❌ 처리 중 오류 발생: ${err.message}`);
        console.log(JSON.stringify({
            success: false,
            message: err.message,
            articleNumbers: [],
            logs
        }));
    }
}

main();
