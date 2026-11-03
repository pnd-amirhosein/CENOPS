// Small same-origin SignalR JSON-protocol *receive-only* client for Blazor Server.
// Chat commands stay in authenticated .NET application services; this connection
// receives server-authorized user events. WebSockets are required (no long-poll fallback).
window.cenopsChatRealtime = (() => {
    const RS = '\x1e';
    let socket = null, bridge = null, reconnectTimer = null, pingTimer = null;
    let active = false, generation = 0, retryCount = 0;
    let invocationId = 0, typingRoom = null, typingAt = 0, typingTimer = null;

    function tell(method, ...args) {
        try { bridge?.invokeMethodAsync(method, ...args)?.catch(() => {}); }
        catch (_) { /* Circuit has been disposed. */ }
    }
    function clearTasks() {
        clearTimeout(reconnectTimer); clearInterval(pingTimer); clearTimeout(typingTimer);
        reconnectTimer = pingTimer = null;
    }
    function schedule(g) {
        if (!active || g !== generation) return;
        clearTasks();
        tell('OnChatConnectionChanged', false);
        const delay = Math.min(15000, 1200 * Math.pow(2, Math.min(retryCount++, 4)));
        reconnectTimer = setTimeout(() => start(g), delay);
    }
    async function start(g) {
        if (!active || g !== generation) return;
        try {
            const res = await fetch('/hubs/chat/negotiate?negotiateVersion=1', {
                method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'text/plain;charset=UTF-8' }, body: ''
            });
            if (!res.ok) throw new Error(`Chat negotiate: HTTP ${res.status}`);
            const info = await res.json();
            if (!info.connectionToken) throw new Error('Invalid SignalR negotiation');
            const url = new URL('/hubs/chat', location.href);
            url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
            url.searchParams.set('id', info.connectionToken);
            const ws = new WebSocket(url.toString());
            socket = ws;
            let handshaken = false, buffer = '';
            ws.onopen = () => { if (active && g === generation) ws.send(JSON.stringify({protocol:'json',version:1}) + RS); };
            ws.onmessage = event => {
                if (!active || g !== generation) return;
                buffer += typeof event.data === 'string' ? event.data : '';
                let index;
                while ((index = buffer.indexOf(RS)) >= 0) {
                    const frame = buffer.slice(0, index); buffer = buffer.slice(index + 1);
                    if (!frame) continue;
                    let message;
                    try { message = JSON.parse(frame); } catch (_) { continue; }
                    if (!handshaken) {
                        if (message.error) { ws.close(); return; }
                        handshaken = true; retryCount = 0;
                        tell('OnChatConnectionChanged', true);
                        pingTimer = setInterval(() => {
                            if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({type:6}) + RS);
                        }, 12000);
                        continue;
                    }
                    if (message.type === 1 && typeof message.target === 'string' && message.target.startsWith('Chat')) {
                        const first = message.arguments?.[0];
                        const roomId = typeof first === 'number' ? first : (typeof first?.roomId === 'number' ? first.roomId : null);
                        if (message.target === 'ChatTyping') {
                            const args = message.arguments || [];
                            tell('OnChatTyping', Number(args[0]), String(args[1] || ''), String(args[2] || ''), args[3] === true);
                        } else if (message.target === 'ChatMessageCreated' && first?.id != null) {
                            tell('OnChatMessageCreated', Number(first.id), Number(first.roomId), String(first.senderUsername || ''), String(first.senderDisplayName || ''), first.content ?? null);
                        } else {
                            tell('OnChatRealtimeEvent', message.target, roomId);
                        }
                    } else if (message.type === 7) { ws.close(); }
                }
            };
            ws.onerror = () => { /* onclose handles retry */ };
            ws.onclose = () => { if (socket === ws) { socket = null; schedule(g); } };
        } catch (_) { schedule(g); }
    }
    function disconnect() {
        stopTyping();
        active = false; generation += 1; clearTasks();
        const ws = socket; socket = null; bridge = null;
        if (ws && ws.readyState < WebSocket.CLOSING) ws.close(1000, 'Workspace closed');
    }
    function connect(dotnet) {
        disconnect(); bridge = dotnet; active = true; retryCount = 0;
        start(generation);
    }
    function invokeHub(method, args) {
        if (!socket || socket.readyState !== WebSocket.OPEN || !active) return;
        socket.send(JSON.stringify({type: 1, invocationId: String(++invocationId), target: method, arguments: args}) + RS);
    }
    function stopTyping() {
        clearTimeout(typingTimer);
        if (typingRoom != null) invokeHub('SetTyping', [typingRoom, false]);
        typingRoom = null;
    }
    function typing(roomId) {
        if (!Number.isFinite(roomId) || roomId <= 0) return;
        if (typingRoom !== roomId) { stopTyping(); typingAt = 0; }
        typingRoom = roomId;
        const now = Date.now();
        if (now - typingAt > 1400) { invokeHub('SetTyping', [roomId, true]); typingAt = now; }
        clearTimeout(typingTimer);
        typingTimer = setTimeout(stopTyping, 2200);
    }
    function bindComposer() {
        const textarea = document.getElementById('cenops-chat-composer');
        if (!textarea || textarea.dataset.chatBound) return;
        textarea.dataset.chatBound = 'yes';
        const resize = () => { textarea.style.height = 'auto'; textarea.style.height = Math.min(180, textarea.scrollHeight) + 'px'; };
        textarea.addEventListener('input', () => {
            resize();
            const roomId = Number(textarea.dataset.chatRoomId);
            if (textarea.value.trim() && textarea.dataset.chatTyping === "true") typing(roomId); else stopTyping();
        });
        textarea.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing && !e.ctrlKey && !e.altKey) {
                e.preventDefault();
                textarea.closest('.chat-composer')?.querySelector('.chat-send:not(:disabled)')?.click();
            }
        });
        resize();
    }
    return {
        connect, disconnect, bindComposer, stopTyping,
        menuPosition: (clientX, clientY) => {
            const modal = document.querySelector('.cenops-chat-window');
            if (!modal) return { x: 12, y: 70 };
            const rect = modal.getBoundingClientRect();
            const menuWidth = 180, menuHeight = 188, inset = 10;
            return {
                x: Math.max(inset, Math.min(clientX - rect.left, rect.width - menuWidth - inset)),
                y: Math.max(47, Math.min(clientY - rect.top, rect.height - menuHeight - inset))
            };
        },
        focusComposer: () => document.getElementById('cenops-chat-composer')?.focus(),
        focusSearch: () => document.getElementById('cenops-chat-global-search')?.focus(),
        showMessage: id => {
            const item = document.getElementById('chat-message-' + id);
            if (!item) return;
            item.scrollIntoView({ block: 'center', behavior: 'smooth' });
            item.classList.add('chat-search-highlight');
            setTimeout(() => item.classList.remove('chat-search-highlight'), 2200);
        },
        scrollBottom: () => { const list = document.getElementById('cenops-chat-message-scroll'); if (list) list.scrollTop = list.scrollHeight; }
    };
})();
