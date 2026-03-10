// Geolocation helper for Blazor
window.getGeolocation = function () {
  return new Promise((resolve) => {
    if (!navigator.geolocation) {
      resolve({
        success: false,
        latitude: 0,
        longitude: 0,
        error: "LOCATION.NOT_SUPPORTED",
      });
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          success: true,
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          error: null,
        });
      },
      (error) => {
        let errorCode;
        switch (error.code) {
          case error.PERMISSION_DENIED:
            errorCode = "LOCATION.PERMISSION_DENIED";
            break;
          case error.POSITION_UNAVAILABLE:
            errorCode = "LOCATION.UNAVAILABLE";
            break;
          case error.TIMEOUT:
            errorCode = "LOCATION.TIMEOUT";
            break;
          default:
            errorCode = "LOCATION.UNKNOWN_ERROR";
        }

        resolve({
          success: false,
          latitude: 0,
          longitude: 0,
          error: errorCode,
        });
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 300000, // 5 minutes cache
      },
    );
  });
};
