// Unity -> host page events for HostBridge.cs.
// The host page (Suburbsim React app) sets window.__suburbsimHost = true before loading the build and
// listens with: window.addEventListener("suburbsim:unity", e => e.detail.type / e.detail.payload).
mergeInto(LibraryManager.library, {

  SuburbsimHostIsAttached: function () {
    return (typeof window !== "undefined" && window.__suburbsimHost === true) ? 1 : 0;
  },

  SuburbsimHostDispatch: function (typePtr, payloadPtr) {
    var type = UTF8ToString(typePtr);
    var payload = UTF8ToString(payloadPtr);
    window.dispatchEvent(new CustomEvent("suburbsim:unity", { detail: { type: type, payload: payload } }));
  }
});
