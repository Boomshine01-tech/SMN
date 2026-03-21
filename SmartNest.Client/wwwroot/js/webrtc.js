window.SmartNestWebRTC = (() => {
    let ICE_SERVERS = [
        { urls: "stun:stun.l.google.com:19302" },
        { urls: "stun:stun1.l.google.com:19302" },
        { urls: "stun:openrelay.metered.ca:80" },
        { urls: "turn:openrelay.metered.ca:80",                username:"openrelayproject", credential:"openrelayproject" },
        { urls: "turn:openrelay.metered.ca:443",               username:"openrelayproject", credential:"openrelayproject" },
        { urls: "turn:openrelay.metered.ca:443?transport=tcp", username:"openrelayproject", credential:"openrelayproject" },
        { urls: "turns:openrelay.metered.ca:443",              username:"openrelayproject", credential:"openrelayproject" }
    ];
    let hub=null, pc=null, stream=null, recorder=null;
    let dotnet=null, roomId=null, token=null, role=null;
    let senderConn=null, viewerConn=null;
    let chunks=[], sessionId=null, recTimer=null;

    const log = (m,...a) => console.log(`[WebRTC] ${m}`,...a);
    const notify = (ev, d) => dotnet?.invokeMethodAsync("OnWebRtcEvent", ev, JSON.stringify(d||{}));

    async function loadIce(t) {
        try {
            const r = await fetch("/api/webrtc/ice-servers", { headers:{"Authorization":"Bearer "+t} });
            if (r.ok) { const d=await r.json(); if(d?.length){ ICE_SERVERS=d; } }
        } catch(e) { log("ICE fallback (openrelay)"); }
    }

    async function connectHub(t) {
        hub = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/webrtc", { accessTokenFactory:()=>t })
            .withAutomaticReconnect([0,2000,5000,15000,30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        hub.on("PeerJoined",    id => { notify("peer-joined",{connId:id}); if(role==="viewer"){senderConn=id; setTimeout(()=>reqOffer(),500);} });
        hub.on("PeerLeft",      id => { notify("peer-left",{connId:id}); closePc(); });
        hub.on("OfferRequested",id => { viewerConn=id; if(role==="sender") sendOffer(id); });
        hub.on("ReceiveOffer",  async(sdp,sid) => { senderConn=sid; await handleOffer(sdp,sid); });
        hub.on("ReceiveAnswer", async sdp => { if(pc?.signalingState!=="stable") await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(sdp))); });
        hub.on("ReceiveIceCandidate", async c => { try{ if(pc) await pc.addIceCandidate(new RTCIceCandidate(JSON.parse(c))); }catch(e){} });
        hub.on("RecordingSaved", fn => notify("recording-saved",{fileName:fn}));
        hub.onreconnected(async() => await hub.invoke("JoinRoom", roomId));
        hub.onclose(() => notify("hub-disconnected",{}));

        await hub.start();
        await hub.invoke("JoinRoom", roomId);
        log("Hub connecté, room:", roomId);
    }

    async function initViewer(room, t, ref) {
        roomId=room; token=t; dotnet=ref; role="viewer";
        await loadIce(t); await connectHub(t);
        notify("hub-connected",{});
        setTimeout(reqOffer, 1000);
    }

    async function reqOffer() {
        if (!hub) return;
        notify("waiting-sender",{});
        await hub.invoke("RequestOffer", roomId);
    }

    async function handleOffer(sdpJson, sid) {
        closePc();
        pc = createPc();
        await pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(sdpJson)));
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        await hub.invoke("SendAnswer", roomId, JSON.stringify(answer), sid);
    }

    async function initSender(room, t, ref) {
        roomId=room; token=t; dotnet=ref; role="sender";
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video:{width:{ideal:1280},height:{ideal:720},frameRate:{ideal:25}}, audio:true
            });
        } catch(e) { notify("error",{message:"Accès caméra refusé : "+e.message}); return false; }
        const prev = document.getElementById("local-preview");
        if (prev) { prev.srcObject=stream; prev.muted=true; prev.play().catch(()=>{}); }
        await loadIce(t); await connectHub(t);
        notify("sender-ready",{});
        return true;
    }

    async function sendOffer(vid) {
        closePc();
        pc = createPc();
        stream.getTracks().forEach(t => pc.addTrack(t, stream));
        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        await hub.invoke("SendOffer", roomId, JSON.stringify(offer), vid);
    }

    function createPc() {
        const p = new RTCPeerConnection({
            iceServers: ICE_SERVERS, iceTransportPolicy:"all",
            bundlePolicy:"max-bundle", rtcpMuxPolicy:"require"
        });
        p.onicecandidate = async e => {
            if (e.candidate && hub)
                await hub.invoke("SendIceCandidate", roomId, JSON.stringify(e.candidate),
                    role==="sender" ? viewerConn : senderConn);
        };
        p.oniceconnectionstatechange = () => {
            notify("ice-state",{state:p.iceConnectionState});
            if (p.iceConnectionState==="connected"||p.iceConnectionState==="completed") {
                p.getStats().then(s => s.forEach(r => {
                    if (r.type==="candidate-pair"&&r.state==="succeeded")
                        notify("connection-type",{type:`${r.localCandidateType}→${r.remoteCandidateType}`});
                }));
            }
        };
        p.onconnectionstatechange = () => notify("connection-state",{state:p.connectionState});
        if (role==="viewer") {
            p.ontrack = e => {
                const v=document.getElementById("remote-video");
                if(v&&e.streams?.[0]){v.srcObject=e.streams[0];v.play().catch(()=>{});notify("stream-received",{});}
            };
        }
        return p;
    }

    async function startRecording(t) {
        if (!stream) return false;
        sessionId=Date.now().toString(); chunks=[];
        const mime=["video/webm;codecs=vp9,opus","video/webm;codecs=vp8,opus","video/webm"]
            .find(x=>MediaRecorder.isTypeSupported(x))||"video/webm";
        recorder=new MediaRecorder(stream,{mimeType:mime,videoBitsPerSecond:2_500_000});
        recorder.ondataavailable=e=>{if(e.data?.size>0)chunks.push(e.data);};
        recTimer=setInterval(()=>upload(t,false),60000);
        recorder.start(1000);
        if(hub) await hub.invoke("NotifyRecordingStarted",roomId,sessionId);
        notify("recording-started",{sessionId});
        return true;
    }

    async function stopRecording(t) {
        if(!recorder)return;
        clearInterval(recTimer);
        await new Promise(r=>{recorder.onstop=r;recorder.stop();});
        await upload(t,true); recorder=null;
        notify("recording-stopped",{});
    }

    async function upload(t,final) {
        if(!chunks.length)return;
        const blob=new Blob(chunks,{type:"video/webm"}); chunks=[];
        const fd=new FormData(); fd.append("file",blob,"recording.webm");
        try {
            const r=await fetch(`/api/video/upload?sessionId=${sessionId}`,{method:"POST",headers:{"Authorization":"Bearer "+t},body:fd});
            if(r.ok){const d=await r.json();if(final&&hub)await hub.invoke("NotifyRecordingSaved",roomId,d.fileName);notify("chunk-uploaded",{fileName:d.fileName,isFinal:final});}
        } catch(e){notify("error",{message:"Upload: "+e.message});}
    }

    function closePc(){if(pc){pc.close();pc=null;}}
    async function stopAll(){
        closePc();
        if(stream){stream.getTracks().forEach(t=>t.stop());stream=null;}
        clearInterval(recTimer);
        if(hub){await hub.stop();hub=null;}
    }

    return { initViewer, initSender, requestOffer:reqOffer, startRecording, stopRecording, stopAll };
})();
