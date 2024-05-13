
window.getTitle = () => {
    return document.title;
};


window.showSpinner = function () {
    document.getElementById("loading-spinner").style.display = "block";
};

window.hideSpinner = function () {
    document.getElementById("loading-spinner").style.display = "none";
};