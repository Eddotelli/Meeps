// Leaflet map instance storage
let maps = {};

/**
 * Initialize a Leaflet map with optional marker and radius circle
 * @param {string} mapId - The container element ID
 * @param {number} latitude - Center latitude
 * @param {number} longitude - Center longitude
 * @param {number} zoomLevel - Zoom level (1-19)
 * @param {boolean} showMarker - Whether to show a marker at center
 * @param {number|null} radiusKm - Radius in kilometers to display as a circle (optional)
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function initializeMap(mapId, latitude, longitude, zoomLevel, showMarker = true, radiusKm = null) {
    try {
        // Clean up existing map if it exists
        if (maps[mapId]) {
            maps[mapId].remove();
            delete maps[mapId];
        }

        // Wait for DOM element to be ready - give it a bit more time
        await new Promise(resolve => setTimeout(resolve, 50));

        const mapElement = document.getElementById(mapId);
        if (!mapElement) {
            return { success: false, error: 'Map container not found' };
        }

        // Create map
        const map = L.map(mapId, {
            center: [latitude, longitude],
            zoom: zoomLevel,

            zoomAnimation: true,
            fadeAnimation: false,
            markerZoomAnimation: false,
            preferCanvas: true,

            trackResize: false,
            scrollWheelZoom: true,
            dragging: true,
            zoomControl: true
        });

        // Create custom pane for event markers with higher z-index
        map.createPane('eventMarkersPane');
        map.getPane('eventMarkersPane').style.zIndex = 650; // Higher than overlayPane (400)

        // Add OpenStreetMap tiles
        // L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        //     maxZoom: 19,
        //     attribution: '© OpenStreetMap contributors'
        // }).addTo(map);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            updateWhenIdle: true,
            updateWhenZooming: false,
            keepBuffer: 2,
            attribution: '© OpenStreetMap contributors'
        }).addTo(map);


        requestAnimationFrame(() => {
            map.invalidateSize(true);
});

        setTimeout(() => {
            map.invalidateSize(true);
        }, 300);


        // Add marker if requested
        if (showMarker) {
            L.marker([latitude, longitude]).addTo(map);
        }

        // Add radius circle if requested
        if (radiusKm !== null && radiusKm > 0) {
            L.circle([latitude, longitude], {
                color: '#dc3545',        // Red color
                fillColor: '#dc3545',
                fillOpacity: 0.15,
                weight: 2,
                radius: radiusKm * 1000  // Convert km to meters
            }).addTo(map);
        }

        // Store map instance
        maps[mapId] = map;

        // Force map to resize after a brief delay
        // await new Promise(resolve => setTimeout(resolve, 100));
        // map.invalidateSize();

        return { success: true };
    } catch (error) {
        console.error('Error initializing map:', error);
        return { success: false, error: error.message || 'Unknown error' };
    }
}

/**
 * Update map center, marker position and radius circle
 * @param {string} mapId - The map ID
 * @param {number} latitude - New center latitude
 * @param {number} longitude - New center longitude
 * @param {number} zoomLevel - New zoom level
 * @param {boolean} showMarker - Whether to show marker
 * @param {number|null} radiusKm - Radius in kilometers to display as a circle (optional)
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function updateMap(mapId, latitude, longitude, zoomLevel, showMarker = true, radiusKm = null) {
    try {
        const map = maps[mapId];
        if (!map) {
            return { success: false, error: 'Map not found' };
        }

        // Get current zoom to preserve user's zoom level
        const currentZoom = map.getZoom();
        const currentCenter = map.getCenter();

        // Only update center/zoom if location actually changed
        if (currentCenter.lat !== latitude || currentCenter.lng !== longitude) {
            map.setView([latitude, longitude], currentZoom);
        }

        // Clear existing markers and circles
        map.eachLayer((layer) => {
            if (layer instanceof L.Marker || layer instanceof L.Circle) {
                map.removeLayer(layer);
            }
        });

        // Add new marker if requested
        if (showMarker) {
            L.marker([latitude, longitude]).addTo(map);
        }

        // Add radius circle if requested
        if (radiusKm !== null && radiusKm > 0) {
            L.circle([latitude, longitude], {
                color: '#dc3545',        // Red color
                fillColor: '#dc3545',
                fillOpacity: 0.15,
                weight: 2,
                radius: radiusKm * 1000,  // Convert km to meters
                pane: 'overlayPane'  // Explicit pane (z-index 400)
            }).addTo(map);
        }

        return { success: true };
    } catch (error) {
        console.error('Error updating map:', error);
        return { success: false, error: error.message || 'Unknown error' };
    }
}

/**
 * Update only the radius circle without affecting map position or zoom
 * @param {string} mapId - The map ID
 * @param {number} latitude - Center latitude for the circle
 * @param {number} longitude - Center longitude for the circle
 * @param {number|null} radiusKm - Radius in kilometers to display as a circle (optional)
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function updateRadius(mapId, latitude, longitude, radiusKm = null) {
    try {
        const map = maps[mapId];
        if (!map) {
            return { success: false, error: 'Map not found' };
        }

        // Remove only circles, keep markers
        map.eachLayer((layer) => {
            if (layer instanceof L.Circle) {
                map.removeLayer(layer);
            }
        });

        // Add new radius circle if requested
        if (radiusKm !== null && radiusKm > 0) {
            L.circle([latitude, longitude], {
                color: '#dc3545',
                fillColor: '#dc3545',
                fillOpacity: 0.15,
                weight: 2,
                radius: radiusKm * 1000,
                pane: 'overlayPane'  // Explicit pane (z-index 400)
            }).addTo(map);
        }

        // Bring event markers to front after radius update
        map.eachLayer((layer) => {
            if (layer instanceof L.CircleMarker && layer.options.isEventMarker) {
                layer.bringToFront();
            }
        });

        return { success: true };
    } catch (error) {
        console.error('Error updating radius:', error);
        return { success: false, error: error.message || 'Unknown error' };
    }
}

/**
 * Update event markers on the map
 * @param {string} mapId - The map ID
 * @param {number} centerLatitude - Center latitude
 * @param {number} centerLongitude - Center longitude
 * @param {Array<{eventHash: string, latitude: number, longitude: number, title: string, distanceKm: number}>} events - Array of events to display
 * @param {object} dotNetHelper - .NET object reference for callbacks
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function updateEventMarkers(mapId, centerLatitude, centerLongitude, events = [], dotNetHelper = null) {
    try {
        console.log('updateEventMarkers called:', { mapId, centerLatitude, centerLongitude, eventCount: events?.length || 0 });
        
        const map = maps[mapId];
        if (!map) {
            console.error('Map not found:', mapId);
            return { success: false, error: 'Map not found' };
        }

        // Remove only event markers (not center marker or circles)
        let removedCount = 0;
        map.eachLayer((layer) => {
            if (layer instanceof L.CircleMarker && layer.options.isEventMarker) {
                map.removeLayer(layer);
                removedCount++;
            }
        });
        console.log('Removed event markers:', removedCount);

        // Add event markers as CircleMarkers
        if (events && events.length > 0) {
            events.forEach(event => {
                if (event.latitude && event.longitude) {
                    // Create CircleMarker for events - always perfectly centered
                    const marker = L.circleMarker([event.latitude, event.longitude], {
                        radius: 8,
                        fillColor: '#594ae2',
                        color: '#ffffff',
                        weight: 2,
                        opacity: 1,
                        fillOpacity: 0.8,
                        isEventMarker: true,
                        pane: 'eventMarkersPane'  // Use custom pane with higher z-index
                    });

                    // Add tooltip that shows on hover
                    marker.bindTooltip(event.title || 'Event', {
                        permanent: false,
                        direction: 'top',
                        offset: [0, -10],
                        className: 'event-tooltip'
                    });

                    // Bring marker to front on hover
                    marker.on('mouseover', function() {
                        this.bringToFront();
                    });

                    // Add popup with more details and clickable link
                    const distanceText = event.distanceKm ? `${event.distanceKm.toFixed(1)} km` : '';
                    
                    const popupContent = `
                        <div class="event-popup" style="font-family: Roboto, Helvetica, Arial, sans-serif; padding: 8px 4px;">
                            <div style="font-size: 15px; font-weight: 500; color: #424242; margin-bottom: 4px;">${event.title || 'Event'}</div>
                            ${distanceText ? `<div style="font-size: 13px; color: #757575; margin-bottom: 8px;">${distanceText}</div>` : ''}
                            ${event.eventHash && dotNetHelper ? `
                                <a href="#" class="event-link" data-eventhash="${event.eventHash}" 
                                   style="display: inline-flex; align-items: center; gap: 4px; color: #594ae2; text-decoration: none; font-size: 14px; font-weight: 500; padding: 6px 12px; border-radius: 4px; background-color: rgba(89, 74, 226, 0.08); transition: background-color 0.2s;"
                                   onmouseover="this.style.backgroundColor='rgba(89, 74, 226, 0.15)'"
                                   onmouseout="this.style.backgroundColor='rgba(89, 74, 226, 0.08)'">
                                    <span>View event</span>
                                    <svg style="width: 16px; height: 16px; fill: currentColor;" viewBox="0 0 24 24">
                                        <path d="M8.59,16.58L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.58Z"/>
                                    </svg>
                                </a>
                            ` : ''}
                        </div>
                    `;
                    
                    const popup = L.popup({
                        maxWidth: 250,
                        className: 'custom-event-popup'
                    }).setContent(popupContent);
                    marker.bindPopup(popup);

                    // Handle click on "View event" link
                    if (event.eventHash && dotNetHelper) {
                        marker.on('popupopen', () => {
                            setTimeout(() => {
                                const link = document.querySelector(`a.event-link[data-eventhash="${event.eventHash}"]`);
                                if (link) {
                                    link.addEventListener('click', (e) => {
                                        e.preventDefault();
                                        dotNetHelper.invokeMethodAsync('NavigateToEvent', event.eventHash);
                                    });
                                }
                            }, 100);
                        });
                    }

                    marker.addTo(map);
                }
            });
            console.log('Total event markers added:', events.length);
        } else {
            console.log('No events to display');
        }

        return { success: true };
    } catch (error) {
        console.error('Error updating event markers:', error);
        return { success: false, error: error.message || 'Unknown error' };
    }
}

/**
 * Dispose a map instance
 * @param {string} mapId - The map ID to dispose
 * @returns {Promise<{success: boolean, error?: string}>}
 */
export async function disposeMap(mapId) {
    try {
        if (maps[mapId]) {
            maps[mapId].remove();
            delete maps[mapId];
        }
        return { success: true };
    } catch (error) {
        console.error('Error disposing map:', error);
        return { success: false, error: error.message || 'Unknown error' };
    }
}
