// =============================================
// PERMISSIONS MANAGER VỚI LOCALSTORAGE
// Lưu trạng thái quyền lâu dài trên thiết bị
// =============================================

const PERMISSION_STORAGE_KEY = 'chamcong_permissions';

// ✅ LƯU TRẠNG THÁI QUYỀN VÀO LOCALSTORAGE
function savePermissionStatus(camera, location) {
    const status = {
        camera: camera,
        location: location,
        timestamp: new Date().toISOString(),
        device: navigator.userAgent
    };
    localStorage.setItem(PERMISSION_STORAGE_KEY, JSON.stringify(status));
    console.log('💾 Đã lưu trạng thái quyền vào LocalStorage:', status);
}

// ✅ ĐỌC TRẠNG THÁI QUYỀN TỪ LOCALSTORAGE
function getStoredPermissionStatus() {
    try {
        const stored = localStorage.getItem(PERMISSION_STORAGE_KEY);
        if (stored) {
            const status = JSON.parse(stored);
            console.log('📖 Đọc trạng thái quyền từ LocalStorage:', status);
            return status;
        }
    } catch (error) {
        console.error('❌ Lỗi đọc LocalStorage:', error);
    }
    return null;
}

// ✅ KIỂM TRA TRẠNG THÁI QUYỀN (ƯU TIÊN BROWSER, SAU ĐÓ LOCALSTORAGE)
window.checkPermissionsStatus = async () => {
    console.log('🔍 Kiểm tra trạng thái quyền (KHÔNG xin quyền)...');

    let cameraStatus = 'unknown';
    let locationStatus = 'unknown';

    try {
        // ✅ 1. THỬ QUERY PERMISSIONS API (CHỈ KIỂM TRA, KHÔNG XIN QUYỀN)
        if (navigator.permissions) {
            try {
                const cameraPermission = await navigator.permissions.query({ name: 'camera' });
                cameraStatus = cameraPermission.state;
                console.log('📹 Camera permission (Browser):', cameraStatus);
            } catch (e) {
                console.warn('⚠️ Không thể query camera permission:', e.message);
            }

            try {
                const locationPermission = await navigator.permissions.query({ name: 'geolocation' });
                locationStatus = locationPermission.state;
                console.log('📍 Location permission (Browser):', locationStatus);
            } catch (e) {
                console.warn('⚠️ Không thể query location permission:', e.message);
            }
        }

        // ✅ 2. NẾU BROWSER KHÔNG HỖ TRỢ, ĐỌC TỪ LOCALSTORAGE
        if (cameraStatus === 'unknown' || locationStatus === 'unknown') {
            const stored = getStoredPermissionStatus();
            if (stored) {
                if (cameraStatus === 'unknown') cameraStatus = stored.camera;
                if (locationStatus === 'unknown') locationStatus = stored.location;
                console.log('💾 Sử dụng trạng thái từ LocalStorage');
            }
        }

        const allGranted = cameraStatus === 'granted' && locationStatus === 'granted';

        console.log('✅ Kết quả kiểm tra:', { camera: cameraStatus, location: locationStatus, allGranted });

        return {
            camera: cameraStatus,
            location: locationStatus,
            allGranted: allGranted
        };
    } catch (error) {
        console.error('❌ Lỗi kiểm tra permissions:', error);
        return {
            camera: 'unknown',
            location: 'unknown',
            allGranted: false
        };
    }
};

// ✅ XIN QUYỀN VÀ LƯU VÀO LOCALSTORAGE
window.requestAllPermissions = async () => {
    console.log('🔐 Người dùng đã nhấn nút "Cấp quyền truy cập"...');

    const result = {
        camera: { success: false, message: '' },
        location: { success: false, message: '' },
        allGranted: false
    };

    try {
        // ===== CAMERA PERMISSION =====
        console.log('📹 Người dùng đã yêu cầu xin quyền camera...');
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
            stream.getTracks().forEach(track => track.stop());
            result.camera.success = true;
            result.camera.message = 'Đã cấp quyền camera';
            console.log('✅ Đã cấp quyền camera');
        } catch (error) {
            result.camera.success = false;
            result.camera.message = error.name === 'NotAllowedError'
                ? 'Người dùng từ chối quyền camera'
                : 'Lỗi khi xin quyền camera: ' + error.message;
            console.error('❌ Lỗi xin quyền camera:', error);
        }

        // ===== LOCATION PERMISSION =====
        console.log('📍 Người dùng đã yêu cầu xin quyền GPS...');
        try {
            const position = await new Promise((resolve, reject) => {
                navigator.geolocation.getCurrentPosition(resolve, reject);
            });
            result.location.success = true;
            result.location.message = 'Đã cấp quyền GPS';
            console.log('✅ Đã cấp quyền GPS:', position.coords);
        } catch (error) {
            result.location.success = false;
            result.location.message = error.code === 1
                ? 'Người dùng từ chối quyền GPS'
                : 'Lỗi khi xin quyền GPS: ' + error.message;
            console.error('❌ Lỗi xin quyền GPS:', error);
        }

        // ✅ LƯU TRẠNG THÁI VÀO LOCALSTORAGE
        const cameraStatus = result.camera.success ? 'granted' : 'denied';
        const locationStatus = result.location.success ? 'granted' : 'denied';
        savePermissionStatus(cameraStatus, locationStatus);

        result.allGranted = result.camera.success && result.location.success;

        if (result.allGranted) {
            console.log('✅ Đã hoàn tất cấp tất cả quyền');
        }

        console.log('📊 Kết quả xin quyền:', result);
        return result;

    } catch (error) {
        console.error('❌ Lỗi xin quyền:', error);
        return result;
    }
};

// ✅ XIN LẠI QUYỀN ĐƠN LẺ (CHO CAMERA HOẶC GPS)
window.requestSinglePermission = async (permissionType) => {
    console.log(`🔐 Xin lại quyền ${permissionType}...`);

    try {
        if (permissionType === 'camera') {
            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
            stream.getTracks().forEach(track => track.stop());

            // Cập nhật LocalStorage
            const stored = getStoredPermissionStatus();
            const locationStatus = stored?.location || 'unknown';
            savePermissionStatus('granted', locationStatus);

            console.log('✅ Đã cấp quyền camera');
            return { success: true, message: 'Đã cấp quyền camera' };
        }
        else if (permissionType === 'location') {
            const position = await new Promise((resolve, reject) => {
                navigator.geolocation.getCurrentPosition(resolve, reject);
            });

            // Cập nhật LocalStorage
            const stored = getStoredPermissionStatus();
            const cameraStatus = stored?.camera || 'unknown';
            savePermissionStatus(cameraStatus, 'granted');

            console.log('✅ Đã cấp quyền GPS');
            return { success: true, message: 'Đã cấp quyền GPS' };
        }
    } catch (error) {
        console.error(`❌ Lỗi xin quyền ${permissionType}:`, error);
        return {
            success: false,
            message: error.name === 'NotAllowedError' || error.code === 1
                ? 'Người dùng từ chối quyền'
                : 'Lỗi: ' + error.message
        };
    }
};

// ✅ XÓA TRẠNG THÁI QUYỀN (CHO DEBUG)
window.clearPermissionStorage = () => {
    localStorage.removeItem(PERMISSION_STORAGE_KEY);
    console.log('🗑️ Đã xóa trạng thái quyền từ LocalStorage');
};

// ===== HƯỚNG DẪN BẬT LẠI QUYỀN =====
window.showPermissionInstructions = (permissionType) => {
    const isIOS = /iPhone|iPad|iPod/i.test(navigator.userAgent);
    const isAndroid = /Android/i.test(navigator.userAgent);

    let instructions = '';

    if (permissionType === 'camera') {
        if (isIOS) {
            instructions = `
📱 Hướng dẫn bật Camera trên iPhone:
1. Mở "Cài đặt" (Settings)
2. Kéo xuống tìm "Safari"
3. Chọn "Camera" → Chọn "Allow"
4. Tải lại trang web này
            `;
        } else if (isAndroid) {
            instructions = `
📱 Hướng dẫn bật Camera trên Android:
1. Mở "Cài đặt" (Settings)
2. Chọn "Ứng dụng" (Apps)
3. Tìm "Chrome" hoặc trình duyệt bạn đang dùng
4. Chọn "Quyền" (Permissions)
5. Bật "Camera"
6. Tải lại trang web này
            `;
        } else {
            instructions = `
🖥️ Hướng dẫn bật Camera trên máy tính:
1. Nhấn vào biểu tượng khóa 🔒 bên trái thanh địa chỉ
2. Tìm "Camera" → Chọn "Allow"
3. Tải lại trang
            `;
        }
    } else if (permissionType === 'location') {
        if (isIOS) {
            instructions = `
📱 Hướng dẫn bật GPS trên iPhone:
1. Mở "Cài đặt" (Settings)
2. Chọn "Quyền riêng tư" (Privacy)
3. Chọn "Dịch vụ vị trí" (Location Services)
4. Bật "Dịch vụ vị trí"
5. Tìm "Safari" → Chọn "Khi sử dụng ứng dụng"
6. Tải lại trang web này
            `;
        } else if (isAndroid) {
            instructions = `
📱 Hướng dẫn bật GPS trên Android:
1. Mở "Cài đặt" (Settings)
2. Chọn "Vị trí" (Location)
3. Bật "Sử dụng vị trí"
4. Vào "Ứng dụng" → "Chrome" → "Quyền" → Bật "Vị trí"
5. Tải lại trang web này
            `;
        } else {
            instructions = `
🖥️ Hướng dẫn bật GPS trên máy tính:
1. Nhấn vào biểu tượng khóa 🔒 bên trái thanh địa chỉ
2. Tìm "Location" → Chọn "Allow"
3. Tải lại trang
            `;
        }
    }

    console.log(instructions);
    return instructions;
};

console.log('✅ Permissions Manager đã sẵn sàng');


window.getOrCreateDeviceId = function () {
    try {
        const key = "chamcong_device_id";
        let id = localStorage.getItem(key);
        if (!id) {
            id = (crypto && crypto.randomUUID) ? crypto.randomUUID() : (Date.now().toString(36) + Math.random().toString(36).slice(2));
            localStorage.setItem(key, id);
        }
        return id;
    } catch {
        return null;
    }
};

 //Hàm lấy thông tin thiết bị chi tiết
window.getDeviceInfo = function () {
    try {
        const ua = navigator.userAgent || "Unknown";
        const platform = navigator.platform || "Unknown";
        const language = navigator.language || "Unknown";
        const screenRes = `${screen.width}x${screen.height}`;

        // Tạo chuỗi device info ngắn gọn nhưng đầy đủ
        const info = `${platform} | ${language} | ${screenRes} | ${ua.substring(0, 120)}`;
        return info;
    } catch (e) {
        console.error("getDeviceInfo error:", e);
        return "Unknown Device";
    }
};

// ✅ Hàm lấy Public IP thực tế của thiết bị
window.getPublicIP = async function () {
    try {
        // Thử dịch vụ 1: ipify.org
        const response = await fetch('https://api.ipify.org?format=json', {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) throw new Error('ipify failed');

        const data = await response.json();
        console.log('✅ Đã lấy IP từ ipify:', data.ip);
        return data.ip || "Unknown";
    } catch (e1) {
        console.warn("⚠️ ipify.org failed, trying ipapi.co...", e1.message);

        try {
            // Thử dịch vụ 2: ipapi.co (fallback)
            const response2 = await fetch('https://ipapi.co/json/', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response2.ok) throw new Error('ipapi failed');

            const data2 = await response2.json();
            console.log('✅ Đã lấy IP từ ipapi.co:', data2.ip);
            return data2.ip || "Unknown";
        } catch (e2) {
            console.warn("⚠️ ipapi.co failed, trying icanhazip...", e2.message);

            try {
                // Thử dịch vụ 3: icanhazip.com (fallback cuối)
                const response3 = await fetch('https://icanhazip.com/', {
                    method: 'GET'
                });

                if (!response3.ok) throw new Error('icanhazip failed');

                const ip3 = (await response3.text()).trim();
                console.log('✅ Đã lấy IP từ icanhazip.com:', ip3);
                return ip3 || "Unknown";
            } catch (e3) {
                console.error("❌ Tất cả dịch vụ IP đều thất bại:", e3.message);
                return "Unknown";
            }
        }
    }
};




