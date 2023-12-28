
const app = {
    instance: false,
    keysDown: []
};

function getImageDimensions(imageSrc) {
    return new Promise((resolve) => {
        const img = new Image();
        img.onload = () => {
            resolve([img.width, img.height]);
        };
        img.src = imageSrc;
    });
}

function init(dotNetHelper) {
    console.log("Initializing");
    app.instance = dotNetHelper;
};

window.onkeydown = (e) => {
    if (!isKeyDown(e.key.toLowerCase())) {
        app.keysDown.push(e.key.toLowerCase());
    }
    if (!app.instance) {
        console.log("app.instance is false");
        return;
    }
    if (!e.repeat) {
        app.instance.invokeMethodAsync('OnKey', e.keyCode, false);
    }
};
window.onkeyup = (e) => {
    app.keysDown = app.keysDown.filter(item => item !== e.key.toLowerCase());
    if (!app.instance) {
        console.log("app.instance is false");
        return;
    }
    if (!e.repeat) {
        app.instance.invokeMethodAsync('OnKey', e.keyCode, true);
    }
};


function isKeyDown(key) {
    return app.keysDown.filter(item => item === key).length !== 0
}

function downloadFile(filename, contentType, content) {
    // Create the URL
    const file = new File([content], filename, { type: contentType });
    const exportUrl = URL.createObjectURL(file);

    // Create the <a> element and click on it
    const a = document.createElement("a");
    document.body.appendChild(a);
    a.href = exportUrl;
    a.download = filename;
    a.target = "_self";
    a.click();

    // We don't need to keep the object URL, let's release the memory
    // On older versions of Safari, it seems you need to comment this line...
    URL.revokeObjectURL(exportUrl);
}