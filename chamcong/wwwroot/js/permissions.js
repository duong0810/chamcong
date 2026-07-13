// =============================================
// PERMISSIONS MANAGER VỚI LOCALSTORAGE
// Lưu trạng thái quyền theo USER + THIẾT BỊ
// =============================================

// ✅ Tạo key riêng biệt cho từng user
function getPermissionKey(userId) {
    return `chamcong_permissions_user_${userId || 'anonymous'}`;
}

// ✅ LƯU TRẠNG THÁI QUYỀN VÀO LOCALSTORAGE (theo userId)
function savePermissionStatus(camera, location, userId) {
    const key = getPermissionKey(userId);
    const status = {
        camera: camera,
        location: location,
        timestamp: new Date().toISOString(),
        device: navigator.userAgent,
        userId: userId
    };
    localStorage.setItem(key, JSON.stringify(status));
    console.log('💾 Đã lưu trạng thái quyền vào LocalStorage:', status);
}

// ✅ ĐỌC TRẠNG THÁI QUYỀN TỪ LOCALSTORAGE (theo userId)
function getStoredPermissionStatus(userId) {
    try {
        const key = getPermissionKey(userId);
        const stored = localStorage.getItem(key);
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


// ✅ Tìm bất kỳ permission đã lưu nào (khi không có userId hoặc sai userId)
function _findAnyStoredPermission() {
    try {
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key && key.startsWith('chamcong_permissions_user_')) {
                const raw = localStorage.getItem(key);
                if (raw) {
                    const parsed = JSON.parse(raw);
                    if (parsed.camera === 'granted' && parsed.location === 'granted') {
                        console.log('🔍 _findAnyStoredPermission: tìm thấy tại key:', key);
                        return parsed;
                    }
                }
            }
        }
    } catch (e) {
        console.warn('_findAnyStoredPermission error:', e);
    }
    return null;
}
// ✅ KIỂM TRA TRẠNG THÁI QUYỀN (ưu tiên Browser, sau đó LocalStorage theo userId)
window.checkPermissionsStatus = async (userId) => {
    console.log('🔍 Kiểm tra trạng thái quyền (KHÔNG xin quyền)...');

    let cameraStatus = 'unknown';
    let locationStatus = 'unknown';

    try {
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

        // ✅ Fallback LocalStorage — thử theo thứ tự ưu tiên
        if (cameraStatus === 'unknown' || cameraStatus === 'prompt' ||
            locationStatus === 'unknown' || locationStatus === 'prompt') {

            // Bước 1: thử đúng userId được truyền vào
            let stored = (userId && userId !== 'undefined' && userId !== 'null')
                ? getStoredPermissionStatus(userId)
                : null;

            // Bước 2: ✅ nếu không có / sai userId → tìm bất kỳ key nào đã lưu
            if (!stored) {
                stored = _findAnyStoredPermission();
            }

            if (stored) {
                if (cameraStatus === 'unknown' || cameraStatus === 'prompt') cameraStatus = stored.camera;
                if (locationStatus === 'unknown' || locationStatus === 'prompt') locationStatus = stored.location;
                console.log('💾 Sử dụng trạng thái từ LocalStorage (userId:', stored.userId, ')');
            }
        }

        const allGranted = cameraStatus === 'granted' && locationStatus === 'granted';
        console.log('✅ Kết quả kiểm tra:', { camera: cameraStatus, location: locationStatus, allGranted });

        return { camera: cameraStatus, location: locationStatus, allGranted };
    } catch (error) {
        console.error('❌ Lỗi kiểm tra permissions:', error);
        return { camera: 'unknown', location: 'unknown', allGranted: false };
    }
};

// ✅ XIN QUYỀN VÀ LƯU VÀO LOCALSTORAGE (theo userId)
window.requestAllPermissions = async (userId) => {
    console.log('🔐 Người dùng đã nhấn nút "Cấp quyền truy cập"...');

    const result = {
        camera: { success: false, message: '' },
        location: { success: false, message: '' },
        allGranted: false
    };

    try {
        // ===== CAMERA PERMISSION =====
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
        try {
            await new Promise((resolve, reject) => {
                navigator.geolocation.getCurrentPosition(resolve, reject);
            });
            result.location.success = true;
            result.location.message = 'Đã cấp quyền GPS';
            console.log('✅ Đã cấp quyền GPS');
        } catch (error) {
            result.location.success = false;
            result.location.message = error.code === 1
                ? 'Người dùng từ chối quyền GPS'
                : 'Lỗi khi xin quyền GPS: ' + error.message;
            console.error('❌ Lỗi xin quyền GPS:', error);
        }

        // ✅ Lưu theo userId
        const cameraStatus = result.camera.success ? 'granted' : 'denied';
        const locationStatus = result.location.success ? 'granted' : 'denied';
        savePermissionStatus(cameraStatus, locationStatus, userId);

        result.allGranted = result.camera.success && result.location.success;
        console.log('📊 Kết quả xin quyền:', result);
        return result;

    } catch (error) {
        console.error('❌ Lỗi xin quyền:', error);
        return result;
    }
};

// ✅ XIN LẠI QUYỀN ĐƠN LẺ (theo userId)
window.requestSinglePermission = async (permissionType, userId) => {
    console.log(`🔐 Xin lại quyền ${permissionType}...`);

    try {
        if (permissionType === 'camera') {
            const stream = await navigator.mediaDevices.getUserMedia({ video: true });
            stream.getTracks().forEach(track => track.stop());
            const stored = getStoredPermissionStatus(userId);
            savePermissionStatus('granted', stored?.location || 'unknown', userId);
            console.log('✅ Đã cấp quyền camera');
            return { success: true, message: 'Đã cấp quyền camera' };
        }
        else if (permissionType === 'location') {
            await new Promise((resolve, reject) => {
                navigator.geolocation.getCurrentPosition(resolve, reject);
            });
            const stored = getStoredPermissionStatus(userId);
            savePermissionStatus(stored?.camera || 'unknown', 'granted', userId);
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

// ✅ XÓA TRẠNG THÁI QUYỀN CỦA 1 USER (khi logout)
window.clearPermissionStorage = (userId) => {
    const key = getPermissionKey(userId);
    localStorage.removeItem(key);
    console.log('🗑️ Đã xóa trạng thái quyền của user:', userId);
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

// ===== LẤY LOCAL IP QUA WEBRTC =====
window.getLocalIp = () => {
    return new Promise((resolve) => {
        try {
            const pc = new RTCPeerConnection({ iceServers: [] });
            const ips = new Set();

            pc.createDataChannel('');
            pc.createOffer()
                .then(offer => pc.setLocalDescription(offer))
                .catch(() => resolve(null));

            pc.onicecandidate = (e) => {
                if (!e || !e.candidate) {
                    // ICE gathering xong
                    pc.close();
                    const localIps = [...ips].filter(ip =>
                        /^(192\.168\.|10\.|172\.(1[6-9]|2\d|3[01])\.)/.test(ip) ||
                        ip.startsWith('fd') // IPv6 local
                    );
                    resolve(localIps.length > 0 ? localIps[0] : ([...ips][0] ?? null));
                    return;
                }

                // Parse IP từ candidate string
                const regex = /([0-9]{1,3}(\.[0-9]{1,3}){3}|[a-f0-9]{1,4}(:[a-f0-9]{0,4}){2,7})/gi;
                const match = regex.exec(e.candidate.candidate);
                if (match) ips.add(match[1]);
            };

            // Timeout fallback sau 3 giây
            setTimeout(() => {
                try { pc.close(); } catch (err) { }
                const localIps = [...ips].filter(ip =>
                    /^(192\.168\.|10\.|172\.(1[6-9]|2\d|3[01])\.)/.test(ip)
                );
                resolve(localIps.length > 0 ? localIps[0] : ([...ips][0] ?? null));
            }, 3000);
        } catch (e) {
            console.warn('getLocalIp error:', e);
            resolve(null);
        }
    });
};



