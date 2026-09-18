// A LiveKit interview call, mounted into a BlazorWebView page.
//
// Vanilla livekit-client, not @livekit/components — there is no React here. The pattern
// mirrors interview-room.tsx on the web (see that file's use of LiveKitRoom): connect,
// enable the local camera and mic, attach whatever tracks show up to <video>/<audio>
// elements, detach them when they go away.
window.gositLiveKit = (function () {
    const LK = window.LivekitClient;
    let room = null;

    async function connect(serverUrl, token, localVideoId, remoteContainerId) {
        room = new LK.Room();

        room.on(LK.RoomEvent.TrackSubscribed, (track) => attach(track, remoteContainerId));
        room.on(LK.RoomEvent.TrackUnsubscribed, (track) => detach(track));

        await room.connect(serverUrl, token);
        await room.localParticipant.setCameraEnabled(true);
        await room.localParticipant.setMicrophoneEnabled(true);

        // The local preview: whichever local video publication just got created.
        for (const publication of room.localParticipant.videoTrackPublications.values()) {
            if (publication.track) {
                attachLocal(publication.track, localVideoId);
            }
        }
    }

    function attach(track, containerId) {
        const container = document.getElementById(containerId);
        if (!container) {
            return;
        }
        const el = track.attach();
        el.dataset.lkTrackSid = track.sid;
        el.style.width = "100%";
        el.style.borderRadius = "12px";
        container.appendChild(el);
    }

    function attachLocal(track, videoId) {
        const el = document.getElementById(videoId);
        if (!el) {
            return;
        }
        track.attach(el);
        el.muted = true; // Never echo someone's own mic back at them.
    }

    function detach(track) {
        track.detach().forEach((el) => el.remove());
    }

    async function setMicEnabled(enabled) {
        if (room) {
            await room.localParticipant.setMicrophoneEnabled(enabled);
        }
    }

    async function setCameraEnabled(enabled) {
        if (room) {
            await room.localParticipant.setCameraEnabled(enabled);
        }
    }

    async function disconnect() {
        if (room) {
            await room.disconnect();
            room = null;
        }
    }

    return { connect, setMicEnabled, setCameraEnabled, disconnect };
})();
