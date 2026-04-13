window.getGeolocation = () => {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error("Trình duyệt không hỗ trợ GPS"));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracy: position.coords.accuracy
                });
            },
            (error) => {
                let message = "Không thể lấy vị trí";
                switch (error.code) {
                    case error.PERMISSION_DENIED:
                        message = "Người dùng từ chối chia sẻ vị trí";
                        break;
                    case error.POSITION_UNAVAILABLE:
                        message = "Thông tin vị trí không khả dụng";
                        break;
                    case error.TIMEOUT:
                        message = "Hết thời gian chờ lấy vị trí";
                        break;
                }
                reject(new Error(message));
            },
            {
                enableHighAccuracy: true,      // Độ chính xác cao
                timeout: 10000,                // Timeout 10s
                maximumAge: 30000              // Cache GPS trong 30 giây (thay vì 0)
            }
        );
    });
};