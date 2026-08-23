import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig(function (_a) {
    var _b;
    var mode = _a.mode;
    var env = loadEnv(mode, '.', '');
    var proxyTarget = (_b = env.VITE_PROXY_TARGET) !== null && _b !== void 0 ? _b : 'http://api';
    return {
        plugins: [react()],
        server: {
            host: '0.0.0.0',
            port: 3000,
            proxy: {
                '/api': {
                    target: proxyTarget,
                    changeOrigin: true
                }
            }
        },
        test: {
            environment: 'jsdom',
            setupFiles: './src/test/setup.ts'
        }
    };
});
