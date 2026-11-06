// Browser file download / upload for BlueprintExport in WebGL builds.
mergeInto(LibraryManager.library, {

  // Saves text content as a file through the browser's normal download flow.
  BlueprintDownloadFile: function (fileNamePtr, contentPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var content = UTF8ToString(contentPtr);

    var blob = new Blob([content], { type: "application/json" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  },

  // Opens the browser's file picker and sends the chosen file's text back to Unity
  // via SendMessage(gameObjectName, callbackMethod, contents).
  BlueprintOpenFilePicker: function (gameObjectNamePtr, callbackMethodPtr, extensionPtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var callbackMethod = UTF8ToString(callbackMethodPtr);
    var extension = UTF8ToString(extensionPtr);

    var input = document.createElement("input");
    input.type = "file";
    input.accept = "." + extension;
    input.style.display = "none";

    input.onchange = function (event) {
      var file = event.target.files && event.target.files[0];
      if (file) {
        var reader = new FileReader();
        reader.onload = function () {
          SendMessage(gameObjectName, callbackMethod, reader.result);
        };
        reader.readAsText(file);
      }
      document.body.removeChild(input);
    };

    document.body.appendChild(input);
    input.click();
  }
});
