
//Wird nicht benötigt mit dem direkten Link!

var map;

window.initMap = function () {
    // Koordinaten für den Mittelpunkt der Karte (hier: New York City)
    var myLatLng = { lat: 48.388309478759766, lng: 10.88779354095459 };

    // Optionen für die Karte
    var mapOptions = {
        center: myLatLng,
        zoom: 15 // Zoom-Level (je höher, desto näher)
    };

    // Karte erstellen und an das HTML-Element mit der ID "map" binden
    map = new google.maps.Map(document.getElementById('map'), mapOptions);

    //// Markierung hinzufügen (optional)
    //var marker = new google.maps.Marker({
    //    position: myLatLng,
    //    map: map,
    //    title: 'Budo Shuren Dojo' // Titel der Markierung
    //});
}


const markerView = new google.maps.marker.AdvancedMarkerView({
    map,
    position: { lat: 48.388309478759766, lng: 10.88779354095459 },
    title: "Budo Shuren Dojo"
});

markerView.addListener('click', ({ domEvent, latLng }) => {
    const { target } = domEvent;
    // Handle the click event.
    infoWindow.open(map, marker);
});
}

// Define the address details
var addressDetails = '<div>' +
    '<h3>Budo Shuren Dojo</h3>' +
    '<p>Außstraße 23 1/5</p>' +
    '<p>86153 Augsburg</p>' +
    '</div>';

// Create a new InfoWindow
var infoWindow = new google.maps.InfoWindow({
    content: addressDetails
});

// Create a marker
var marker = new google.maps.Marker({
    position: { lat: 48.388309478759766, lng: 10.88779354095459 },
    map: map, // assuming 'map' is your map object
    title: 'Budo Shuren Dojo' // Title for the marker
});

// Add a click event listener to the marker
marker.addListener('click', function () {
    // Open the InfoWindow when the marker is clicked
    infoWindow.open(map, marker);
});


//// Search for Google's office in Australia.
//var request = {
//    location: map.getCenter(),
//    radius: '500',
//    query: 'Budo Shuren Dojo Augsburg'
//};

//var service = new google.maps.places.PlacesService(map);
//service.textSearch(request, callback);