// =============================================
// Face Recognition Helper cho Blazor
// Sử dụng Face-API.js
// =============================================

let modelsLoaded = false;
window._ssdAvailable = false;

// Load models từ CDN (hoặc điều chỉnh đường dẫn /models/ nếu bạn phục vụ local)
async function loadFaceModels() {
    if (modelsLoaded) return true;

    try {
        const MODEL_URL = 'https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model/';

        console.log('⏳ Đang tải Face-API models...');

        await faceapi.nets.tinyFaceDetector.loadFromUri(MODEL_URL);

        try {
            await faceapi.nets.faceLandmark68TinyNet.loadFromUri(MODEL_URL);
            console.log('✅ faceLandmark68TinyNet loaded');
        } catch (e) {
            console.warn('⚠️ faceLandmark68TinyNet not available, loading full landmark net as fallback', e);
            await faceapi.nets.faceLandmark68Net.loadFromUri(MODEL_URL);
            console.log('✅ faceLandmark68Net loaded as fallback');
        }

        await faceapi.nets.faceRecognitionNet.loadFromUri(MODEL_URL);

        try {
            await faceapi.nets.ssdMobilenetv1.loadFromUri(MODEL_URL);
            window._ssdAvailable = true;
            console.log('✅ ssdMobilenetv1 loaded (fallback available)');
        } catch (e) {
            window._ssdAvailable = false;
            console.warn('⚠️ ssdMobilenetv1 not loaded (will use TinyFaceDetector only)', e);
        }

        modelsLoaded = true;
        console.log('✅ Face-API models đã tải xong');
        return true;
    } catch (error) {
        console.error('❌ Lỗi khi tải face models:', error);
        return false;
    }
}

// ===== Global state =====
window._activeStreams = window._activeStreams || {};
window._livenessRafId = null;
window._livenessIntervalId = null;
window._livenessDotNetRef = null;
window._livenessRunning = false;

let livenessStep = 0;
let lastWarningMessage = '';
let lastWarningTimestamp = 0;
let livenessBlazorRef = null;

let livenessDetectionInterval = null;

// ===== CÁC HÀNH ĐỘNG NGẪU NHIÊN CHO LIVENESS =====
const LIVENESS_ACTIONS = [
    {
        id: 'look_left',
        name: 'Quay đầu sang TRÁI',
        icon: '↩️',
        check: (pose) => pose.yaw < -10,
        hint: (pose) => {
            if (pose.yaw > -2.5) return 'Quay sang trái...';
            if (pose.yaw >= -10 && pose.yaw <= -2.5) return 'Quay sang trái...';
            return '';
        }
    },
    {
        id: 'look_right',
        name: 'Quay đầu sang PHẢI',
        icon: '↪️',
        check: (pose) => pose.yaw > 10,
        hint: (pose) => {
            if (pose.yaw < 2.5) return 'Quay sang phải...';
            if (pose.yaw >= 2.5 && pose.yaw <= 10) return 'Quay sang phải...';
            return '';
        }
    },
    {
        id: 'move_close',
        name: 'Đưa mặt LẠI GẦN',
        icon: '🔍',
        check: (faceSize) => faceSize > 0.18 && faceSize < 0.30,
        hint: (faceSize) => {
            if (faceSize < 0.18) return 'Lại gần hơn nữa...';
            if (faceSize >= 0.30) return 'Quá gần, lùi ra một chút';
            return '';
        }
    },
    {
        id: 'move_far',
        name: 'LÙI MẶT ra xa',
        icon: '🔙',
        check: (faceSize) => faceSize > 0.04 && faceSize < 0.12,
        hint: (faceSize) => {
            if (faceSize >= 0.12) return 'Lùi xa hơn nữa...';
            if (faceSize <= 0.04) return 'Quá xa, lại gần một chút';
            return '';
        }
    }
];

let randomActions = [];
let currentActionIndex = 0;
const NUM_RANDOM_ACTIONS = 2; // Số lượng hành động ngẫu nhiên

// Hàm tạo danh sách hành động ngẫu nhiên
function generateRandomActions() {
    const shuffled = [...LIVENESS_ACTIONS].sort(() => Math.random() - 0.5);
    randomActions = shuffled.slice(0, NUM_RANDOM_ACTIONS);
    currentActionIndex = 0;
    console.log('🎲 Các hành động ngẫu nhiên:', randomActions.map(a => a.name).join(' → '));
    return randomActions;
}

// ===== Overlay size helper - CHO ATTENDANCE (RANDOM ACTIONS) =====
const __overlaySizeConfig = {
    // Kích thước theo loại hành động
    default: 0.50,       // Kích thước mặc định (bước 0 - nhìn thẳng)
    moveClose: 0.60,     // Khi yêu cầu lại gần
    moveFar: 0.40,       // Khi yêu cầu lùi xa
    lookSide: 0.60,      // Khi quay trái/phải (giữ nguyên)
    complete: 0.60,      // Khi hoàn tất
    minPx: 64,           // Kích thước tối thiểu
    maxWrapperPercent: 0.92 // Tối đa so với wrapper width
};

function setOverlaySizeForAction(video, actionId = null, isComplete = false) {
    try {
        if (!video) return;

        // Tìm SVG overlay
        let wrapper = video.closest ? video.closest('.video-wrapper') : null;
        if (!wrapper) wrapper = video.parentElement?.parentElement || video.parentElement || document.body;

        const svg = wrapper?.querySelector('.face-guide-frame');
        const fallbackSvg = document.querySelector('.face-guide-frame');
        const target = svg || fallbackSvg;

        if (!target) {
            console.warn('setOverlaySizeForAction: SVG overlay not found');
            return;
        }

        const wrapperRect = wrapper ? wrapper.getBoundingClientRect() :
            { width: window.innerWidth, height: window.innerHeight };
        const base = wrapperRect.width || Math.min(window.innerWidth, window.innerHeight);

        // Xác định tỷ lệ dựa trên hành động hiện tại
        let ratio;

        if (isComplete) {
            ratio = __overlaySizeConfig.complete;
            hideDirectionArrow(video); // Ẩn mũi tên khi hoàn tất
        } else if (!actionId) {
            // Bước 0: chưa bắt đầu hành động
            ratio = __overlaySizeConfig.default;
            hideDirectionArrow(video); // Ẩn mũi tên ở bước 0
        } else {
            // Chọn kích thước theo loại hành động
            switch (actionId) {
                case 'move_close':
                    ratio = __overlaySizeConfig.moveClose;
                    hideDirectionArrow(video); // Không cần mũi tên cho move_close
                    break;
                case 'move_far':
                    ratio = __overlaySizeConfig.moveFar;
                    hideDirectionArrow(video); // Không cần mũi tên cho move_far
                    break;
                case 'look_left':
                    ratio = __overlaySizeConfig.lookSide;
                    showDirectionArrow(video, 'look_left'); // Hiện mũi tên trái
                    break;
                case 'look_right':
                    ratio = __overlaySizeConfig.lookSide;
                    showDirectionArrow(video, 'look_right'); // Hiện mũi tên phải
                    break;
                default:
                    ratio = __overlaySizeConfig.default;
                    hideDirectionArrow(video);
            }
        }

        let widthPx = Math.round(base * ratio);
        const maxPx = Math.round(wrapperRect.width * __overlaySizeConfig.maxWrapperPercent);
        widthPx = Math.max(__overlaySizeConfig.minPx, Math.min(widthPx, maxPx));

        // Áp dụng transition mượt mà
        target.style.transition = 'width 300ms ease-out, height 300ms ease-out';
        target.style.width = widthPx + 'px';
        target.style.maxWidth = widthPx + 'px';

        console.log(`🔄 Overlay resize (Attendance): ${actionId || 'default'} → ${widthPx}px (${(ratio * 100).toFixed(0)}%)`);
    } catch (e) {
        console.warn('setOverlaySizeForAction error', e);
    }
}

// ===== HÀM SET OVERLAY SIZE CHO REGISTER (5 BƯỚC CỐ ĐỊNH) =====
function setOverlaySizeForRegisterStep(video, step) {
    try {
        if (!video) return;

        let wrapper = video.closest ? video.closest('.video-wrapper') : null;
        if (!wrapper) wrapper = video.parentElement?.parentElement || video.parentElement || document.body;

        const svg = wrapper?.querySelector('.face-guide-frame');
        const fallbackSvg = document.querySelector('.face-guide-frame');
        const target = svg || fallbackSvg;

        if (!target) {
            console.warn('setOverlaySizeForRegisterStep: SVG overlay not found');
            return;
        }

        const wrapperRect = wrapper ? wrapper.getBoundingClientRect() :
            { width: window.innerWidth, height: window.innerHeight };
        const base = wrapperRect.width || Math.min(window.innerWidth, window.innerHeight);

        let ratio;
        // Kích thước theo bước cố định
        if (step === 0) {
            ratio = 0.50;
            hideDirectionArrow(video);
        } else if (step === 1) {
            ratio = 0.60;
            hideDirectionArrow(video);
        } else if (step === 2) {
            ratio = 0.40;
            hideDirectionArrow(video);
        } else if (step === 3) {
            ratio = 0.60;
            showDirectionArrow(video, 'look_left'); // Hiện mũi tên trái
        } else if (step === 4) {
            ratio = 0.60;
            showDirectionArrow(video, 'look_right'); // Hiện mũi tên phải
        } else if (step === 5) {
            ratio = 0.60;
            hideDirectionArrow(video);
        } else {
            ratio = 0.55;
            hideDirectionArrow(video);
        }

        let widthPx = Math.round(base * ratio);
        const maxPx = Math.round(wrapperRect.width * 0.92);
        widthPx = Math.max(64, Math.min(widthPx, maxPx));

        target.style.transition = 'width 300ms ease-out, height 300ms ease-out';
        target.style.width = widthPx + 'px';
        target.style.maxWidth = widthPx + 'px';

        console.log(`🔄 Overlay resize (Register): Step ${step} → ${widthPx}px (${(ratio * 100).toFixed(0)}%)`);
    } catch (e) {
        console.warn('setOverlaySizeForRegisterStep error', e);
    }
}

// ----- Start webcam (inline mode) -----
window.startWebcam = async (videoElementId) => {
    try {
        const video = document.getElementById(videoElementId);
        if (!video) throw new Error('Video element not found: ' + videoElementId);

        try {
            video.setAttribute('playsinline', '');
            video.setAttribute('webkit-playsinline', '');
            video.playsInline = true;
            video.muted = true;
            video.autoplay = true;
            video.style.width = '100%';
            video.style.height = 'auto';
            video.style.objectFit = 'cover';

            try {
                video.style.transform = 'scaleX(-1)';
                video.style.webkitTransform = 'scaleX(-1)';
            } catch (e) {
                console.warn('Could not set video transform', e);
            }
        } catch (e) {
            console.warn('Could not set video inline attributes', e);
        }

        const stream = await navigator.mediaDevices.getUserMedia({
            video: {
                width: { ideal: 640 },
                height: { ideal: 480 },
                facingMode: 'user'
            },
            audio: false
        });

        window._activeStreams[videoElementId] = stream;

        try {
            if ('srcObject' in video) {
                video.srcObject = stream;
            } else {
                video.src = window.URL.createObjectURL(stream);
            }
        } catch (e) {
            console.warn('attach srcObject error, will try assignment and play', e);
            try { video.srcObject = stream; } catch (err) { console.warn('second attempt attach failed', err); }
        }

        video.onloadedmetadata = async () => {
            try {
                await video.play();
            } catch (err) {
                console.warn('video.play() rejected', err);
            }
        };

        try {
            await video.play();
        } catch (e) {
            // ignore
        }

        console.log('✅ Webcam đã bật (inline mode)', videoElementId);
        return true;
    } catch (error) {
        console.error('❌ Lỗi khi bật webcam:', error);
        throw new Error('Không thể bật camera: ' + (error && error.message ? error.message : error));
    }
};

// ----- Stop webcam and cleanup video element -----
window.stopWebcam = (videoElementId) => {
    try {
        try { window.stopLivenessDetection(); } catch (e) { console.warn('stopLivenessDetection err', e); }

        const stopStreamObj = (id, stream) => {
            if (!stream) return;
            try {
                const tracks = stream.getTracks();
                tracks.forEach(track => {
                    try {
                        track.stop();
                        console.log(`⏹️ Đã dừng track (${id}): ${track.kind}`);
                    } catch (e) {
                        console.warn('Error stopping track', e);
                    }
                });
            } catch (e) {
                console.warn('Error accessing tracks', e);
            }
        };

        if (!videoElementId) {
            if (window._activeStreams) {
                Object.keys(window._activeStreams).forEach(id => {
                    const s = window._activeStreams[id];
                    stopStreamObj(id, s);
                    delete window._activeStreams[id];
                });
            }
            document.querySelectorAll('video').forEach(v => { try { if (v.srcObject) { stopStreamObj('dom', v.srcObject); v.srcObject = null; v.style.transform = ''; v.style.webkitTransform = ''; } } catch (e) { } });
            console.log('✅ stopWebcam: stopped all streams');
            return;
        }

        const video = document.getElementById(videoElementId);

        if (video) {
            try { video.style.transform = ''; video.style.webkitTransform = ''; } catch (e) { /* ignore */ }
        }

        if (video && video.srcObject) {
            stopStreamObj(videoElementId, video.srcObject);
            try { video.srcObject = null; } catch (e) { console.warn('clear srcObject', e); }
        }

        const stored = window._activeStreams && window._activeStreams[videoElementId];
        if (stored && stored.getTracks) {
            stopStreamObj(videoElementId, stored);
            delete window._activeStreams[videoElementId];
        }

        if (video && video.parentElement) {
            const canvas = video.parentElement.querySelector('canvas');
            if (canvas) {
                canvas.remove();
                console.log('🗑️ Đã xóa canvas overlay');
            }
        }

        console.log('✅ Webcam đã tắt và cleared', videoElementId);
    } catch (error) {
        console.error('❌ Lỗi tắt webcam:', error);
    }
};

// ----- Capture face descriptor -----
window.captureFaceDescriptor = async (videoElementId) => {
    try {
        const loaded = await loadFaceModels();
        if (!loaded) throw new Error('Không thể tải face models');

        const video = document.getElementById(videoElementId) || {};
        if (!video) throw new Error('Không tìm thấy video element');

        console.log('📸 Đang phát hiện khuôn mặt...');

        const detection = await faceapi
            .detectSingleFace(video, new faceapi.TinyFaceDetectorOptions())
            .withFaceLandmarks(true)
            .withFaceDescriptor();

        if (!detection) {
            throw new Error('Không phát hiện khuôn mặt. Vui lòng nhìn thẳng vào camera.');
        }

        const raw = Array.from(detection.descriptor);
        let sumSq = 0;
        for (let i = 0; i < raw.length; i++) sumSq += raw[i] * raw[i];
        const norm = Math.sqrt(sumSq) || 1.0;
        const descriptor = raw.map(v => v / norm);

        console.log('✅ Đã chụp khuôn mặt, descriptor length:', descriptor.length, 'score:', detection.detection.score);

        return {
            descriptor: descriptor,
            score: detection.detection.score
        };
    } catch (error) {
        console.error('❌ Lỗi khi chụp khuôn mặt:', error);
        throw error;
    }
};

// ----- Compare descriptors -----
window.compareFaceDescriptors = (descriptor1Array, descriptor2Array) => {
    try {
        if (!descriptor1Array || !descriptor2Array || descriptor1Array.length !== descriptor2Array.length)
            throw new Error('Invalid descriptors');

        const len = descriptor1Array.length;

        const normalize = (arr) => {
            let s = 0;
            for (let i = 0; i < arr.length; i++) s += arr[i] * arr[i];
            const n = Math.sqrt(s) || 1.0;
            const out = new Array(arr.length);
            for (let i = 0; i < arr.length; i++) out[i] = arr[i] / n;
            return out;
        };

        const a = normalize(descriptor1Array);
        const b = normalize(descriptor2Array);

        let dot = 0;
        for (let i = 0; i < len; i++) dot += a[i] * b[i];

        const similarity = Math.max(0, dot) * 100;

        const COSINE_THRESHOLD = 0.70; //ngưỡng
        const isMatch = dot >= COSINE_THRESHOLD;

        const distance = 1 - ((dot + 1) / 2);

        return { distance: distance, similarity: similarity, isMatch: isMatch };
    } catch (err) {
        console.error('❌ compareFaceDescriptors error', err);
        return { distance: 1.0, similarity: 0, isMatch: false };
    }
};

// ===== LIVENESS DETECTION HELPERS =====
function euclideanDistance(point1, point2) {
    const dx = point1.x - point2.x;
    const dy = point1.y - point2.y;
    return Math.sqrt(dx * dx + dy * dy);
}

function isElementMirrored(el) {
    try {
        if (!el) return false;
        const inline = el.style && (el.style.transform || el.style.webkitTransform);
        if (inline && (inline.includes('scaleX(-1)') || inline.includes('matrix(-1'))) return true;
        const cs = window.getComputedStyle(el);
        const transform = cs && (cs.transform || cs.webkitTransform);
        if (transform && transform !== 'none') {
            return transform.includes('matrix(-1') || transform.includes('scaleX(-1)');
        }
    } catch (e) {
        // ignore
    }
    return false;
}

function calculateHeadPose(landmarks, video = null) {
    try {
        const nose = landmarks.getNose();
        const leftEye = landmarks.getLeftEye();
        const rightEye = landmarks.getRightEye();

        const leftEyeCenter = {
            x: leftEye.reduce((sum, p) => sum + p.x, 0) / leftEye.length,
            y: leftEye.reduce((sum, p) => sum + p.y, 0) / leftEye.length
        };

        const rightEyeCenter = {
            x: rightEye.reduce((sum, p) => sum + p.x, 0) / rightEye.length,
            y: rightEye.reduce((sum, p) => sum + p.y, 0) / rightEye.length
        };

        const eyesCenter = {
            x: (leftEyeCenter.x + rightEyeCenter.x) / 2,
            y: (leftEyeCenter.y + rightEyeCenter.y) / 2
        };

        const nosePoint = nose[3] || nose[0];
        const eyesDistance = euclideanDistance(leftEyeCenter, rightEyeCenter);
        const noseToEyesCenterX = nosePoint.x - eyesCenter.x;

        const yaw = noseToEyesCenterX / (eyesDistance / 2);
        let yawDegrees = yaw * 30;

        if (isElementMirrored(video)) {
            yawDegrees = -yawDegrees;
        }

        return {
            yaw: yawDegrees,
            pitch: 0,
            roll: 0
        };
    } catch (error) {
        console.error('❌ Lỗi tính head pose:', error);
        return { yaw: 0, pitch: 0, roll: 0 };
    }
}

// ===== LIVENESS DETECTION CHO ATTENDANCE (Random 2/4 hành động) =====
window.startLivenessDetection = async (videoElementId, dotNetRef) => {
    try {
        window.stopLivenessDetection();

        const loaded = await loadFaceModels();
        if (!loaded) {
            console.error('❌ Không thể load face models');
            return;
        }

        const video = document.getElementById(videoElementId);
        if (!video) {
            console.error('❌ Không tìm thấy video element');
            return;
        }

        livenessBlazorRef = dotNetRef;
        window._livenessDotNetRef = dotNetRef;
        livenessStep = 0;
        lastWarningMessage = '';
        lastWarningTimestamp = 0;

        generateRandomActions(); // Random 2 hành động

        console.log('✅ Bắt đầu Liveness Detection - ATTENDANCE MODE (Random)');

        try {
            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep',
                0, 'Đang chờ khuôn mặt hợp lệ...');
        } catch (e) { console.warn('invoke UpdateLivenessStep failed', e); }

        setOverlaySizeForAction(video, null, false);

        livenessDetectionInterval = setInterval(async () => {
            try {
                if (!video.videoWidth || !video.videoHeight || video.readyState < 2) {
                    return;
                }

                const tinyOptionsPrimary = new faceapi.TinyFaceDetectorOptions({ inputSize: 416, scoreThreshold: 0.35 });
                let detection = await faceapi.detectSingleFace(video, tinyOptionsPrimary).withFaceLandmarks(true);

                if (!detection) {
                    await new Promise(r => setTimeout(r, 80));
                    const tinyOptionsRetry = new faceapi.TinyFaceDetectorOptions({ inputSize: 320, scoreThreshold: 0.30 });
                    detection = await faceapi.detectSingleFace(video, tinyOptionsRetry).withFaceLandmarks(true);
                }

                if (!detection && window._ssdAvailable) {
                    detection = await faceapi.detectSingleFace(video, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.35 })).withFaceLandmarks(true);
                }

                if (!detection) {
                    const warningMsg = 'Đang chờ khuôn mặt hợp lệ...';
                    const now = Date.now();
                    if (lastWarningMessage !== warningMsg || (now - lastWarningTimestamp) > 1500) {
                        lastWarningMessage = warningMsg;
                        lastWarningTimestamp = now;
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', livenessStep, warningMsg);
                        } catch (e) { }
                    }
                    return;
                }

                lastWarningMessage = '';
                lastWarningTimestamp = 0;

                const box = detection.detection.box;
                const videoWidth = video.videoWidth;
                const videoHeight = video.videoHeight;
                const faceSize = (box.width * box.height) / (videoWidth * videoHeight);
                const faceCenterX = box.x + box.width / 2;
                const faceCenterY = box.y + box.height / 2;
                const offsetX = Math.abs(faceCenterX - videoWidth / 2);
                const offsetY = Math.abs(faceCenterY - videoHeight / 2);
                const pose = calculateHeadPose(detection.landmarks, video);

                if (livenessStep === 0) {
                    if (faceSize > 0.08 && faceSize < 0.25 &&
                        offsetX < videoWidth * 0.12 && offsetY < videoHeight * 0.12 &&
                        Math.abs(pose.yaw) < 12) {

                        livenessStep = 1;
                        currentActionIndex = 0;
                        const firstAction = randomActions[0];
                        console.log(`✅ Bước khởi đầu hoàn thành - Chuyển sang: ${firstAction.name}`);

                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 1, `${firstAction.icon} ${firstAction.name}`);
                        } catch (e) { }

                        setOverlaySizeForAction(video, firstAction.id, false);
                    }
                }
                else if (livenessStep > 0 && livenessStep <= NUM_RANDOM_ACTIONS) {
                    const action = randomActions[currentActionIndex];
                    let actionCompleted = false;

                    if (action.id === 'look_left' || action.id === 'look_right') {
                        actionCompleted = action.check(pose);
                    } else if (action.id === 'move_close' || action.id === 'move_far') {
                        actionCompleted = action.check(faceSize);
                    }

                    if (actionCompleted) {
                        currentActionIndex++;
                        livenessStep++;

                        if (currentActionIndex >= randomActions.length) {
                            console.log('✅ Hoàn tất tất cả hành động ngẫu nhiên!');
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep',
                                    NUM_RANDOM_ACTIONS + 1, '✅ Hoàn tất! Giữ yên để chấm công');
                            } catch (e) { }
                            setOverlaySizeForAction(video, null, true);
                        } else {
                            const nextAction = randomActions[currentActionIndex];
                            console.log(`✅ Hành động hoàn thành - Chuyển sang: ${nextAction.name}`);
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep',
                                    livenessStep, `${nextAction.icon} ${nextAction.name}`);
                            } catch (e) { }
                            setOverlaySizeForAction(video, nextAction.id, false);
                        }
                    } else {
                        let hint = '';
                        if (action.id === 'look_left' || action.id === 'look_right') {
                            hint = action.hint(pose);
                        } else if (action.id === 'move_close' || action.id === 'move_far') {
                            hint = action.hint(faceSize);
                        }

                        if (hint && lastWarningMessage !== hint) {
                            lastWarningMessage = hint;
                            lastWarningTimestamp = Date.now();
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep',
                                    livenessStep, `${action.icon} ${hint}`);
                            } catch (e) { }
                        }
                    }
                }
            } catch (error) {
                console.error('❌ Lỗi trong liveness detection:', error);
            }
        }, 300);

        console.log('✅ startLivenessDetection started (Attendance mode)');
    } catch (error) {
        console.error('❌ Lỗi khởi động liveness detection:', error);
    }
};

// ===== LIVENESS DETECTION CHO REGISTER FACE (5 bước cố định) =====
window.startLivenessDetectionRegister = async (videoElementId, dotNetRef) => {
    try {
        window.stopLivenessDetection();

        const loaded = await loadFaceModels();
        if (!loaded) {
            console.error('❌ Không thể load face models');
            return;
        }

        const video = document.getElementById(videoElementId);
        if (!video) {
            console.error('❌ Không tìm thấy video element');
            return;
        }

        livenessBlazorRef = dotNetRef;
        window._livenessDotNetRef = dotNetRef;
        livenessStep = 0;
        lastWarningMessage = '';
        lastWarningTimestamp = 0;

        console.log('✅ Bắt đầu Liveness Detection - REGISTER MODE (5 bước cố định)');

        try {
            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep',
                0, 'Đang chờ khuôn mặt hợp lệ...');
        } catch (e) { console.warn('invoke UpdateLivenessStep failed', e); }

        setOverlaySizeForRegisterStep(video, 0);

        livenessDetectionInterval = setInterval(async () => {
            try {
                if (!video.videoWidth || !video.videoHeight || video.readyState < 2) {
                    return;
                }

                const tinyOptionsPrimary = new faceapi.TinyFaceDetectorOptions({ inputSize: 416, scoreThreshold: 0.35 });
                let detection = await faceapi.detectSingleFace(video, tinyOptionsPrimary).withFaceLandmarks(true);

                if (!detection) {
                    await new Promise(r => setTimeout(r, 80));
                    const tinyOptionsRetry = new faceapi.TinyFaceDetectorOptions({ inputSize: 320, scoreThreshold: 0.30 });
                    detection = await faceapi.detectSingleFace(video, tinyOptionsRetry).withFaceLandmarks(true);
                }

                if (!detection && window._ssdAvailable) {
                    detection = await faceapi.detectSingleFace(video, new faceapi.SsdMobilenetv1Options({ minConfidence: 0.35 })).withFaceLandmarks(true);
                }

                if (!detection) {
                    const warningMsg = 'Đang chờ khuôn mặt hợp lệ...';
                    const now = Date.now();
                    if (lastWarningMessage !== warningMsg || (now - lastWarningTimestamp) > 1500) {
                        lastWarningMessage = warningMsg;
                        lastWarningTimestamp = now;
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', livenessStep, warningMsg);
                        } catch (e) { }
                    }
                    return;
                }

                lastWarningMessage = '';
                lastWarningTimestamp = 0;

                const box = detection.detection.box;
                const videoWidth = video.videoWidth;
                const videoHeight = video.videoHeight;
                const faceSize = (box.width * box.height) / (videoWidth * videoHeight);
                const faceCenterX = box.x + box.width / 2;
                const faceCenterY = box.y + box.height / 2;
                const offsetX = Math.abs(faceCenterX - videoWidth / 2);
                const offsetY = Math.abs(faceCenterY - videoHeight / 2);
                const pose = calculateHeadPose(detection.landmarks, video);

                // ===== BƯỚC 0: Nhìn thẳng =====
                if (livenessStep === 0) {
                    if (faceSize > 0.08 && faceSize < 0.25 &&
                        offsetX < videoWidth * 0.12 && offsetY < videoHeight * 0.12 &&
                        Math.abs(pose.yaw) < 12) {

                        livenessStep = 1;
                        console.log('✅ Bước 0 hoàn thành → Bước 1: Lại gần');
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 1, '🔍 Đưa mặt LẠI GẦN');
                        } catch (e) { }
                        setOverlaySizeForRegisterStep(video, 1);
                    }
                }
                // ===== BƯỚC 1: Lại gần =====
                else if (livenessStep === 1) {
                    if (faceSize > 0.18 && faceSize < 0.30) {
                        livenessStep = 2;
                        console.log('✅ Bước 1 hoàn thành → Bước 2: Lùi xa');
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 2, '🔙 LÙI MẶT ra xa');
                        } catch (e) { }
                        setOverlaySizeForRegisterStep(video, 2);
                    } else if (faceSize < 0.18) {
                        const hint = 'Lại gần hơn nữa...';
                        if (lastWarningMessage !== hint) {
                            lastWarningMessage = hint;
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 1, `🔍 ${hint}`);
                            } catch (e) { }
                        }
                    }
                }
                // ===== BƯỚC 2: Lùi xa =====
                else if (livenessStep === 2) {
                    if (faceSize > 0.04 && faceSize < 0.12) {
                        livenessStep = 3;
                        console.log('✅ Bước 2 hoàn thành → Bước 3: Quay trái');
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 3, '↩️ Quay đầu sang TRÁI');
                        } catch (e) { }
                        setOverlaySizeForRegisterStep(video, 3);
                    } else if (faceSize >= 0.12) {
                        const hint = 'Lùi xa hơn nữa...';
                        if (lastWarningMessage !== hint) {
                            lastWarningMessage = hint;
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 2, `🔙 ${hint}`);
                            } catch (e) { }
                        }
                    }
                }
                // ===== BƯỚC 3: Quay trái =====
                else if (livenessStep === 3) {
                    if (pose.yaw < -10) {
                        livenessStep = 4;
                        console.log('✅ Bước 3 hoàn thành → Bước 4: Quay phải');
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 4, '↪️ Quay đầu sang PHẢI');
                        } catch (e) { }
                        setOverlaySizeForRegisterStep(video, 4);
                    } else if (pose.yaw > -2.5) {
                        const hint = 'Quay trái nhiều hơn...';
                        if (lastWarningMessage !== hint) {
                            lastWarningMessage = hint;
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 3, `↩️ ${hint}`);
                            } catch (e) { }
                        }
                    }
                }
                // ===== BƯỚC 4: Quay phải =====
                else if (livenessStep === 4) {
                    if (pose.yaw > 10) {
                        livenessStep = 5;
                        console.log('✅ Bước 4 hoàn thành → Hoàn tất!');
                        try {
                            await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 5, '✅ Hoàn tất! Giữ yên để chụp');
                        } catch (e) { }
                        setOverlaySizeForRegisterStep(video, 5);
                    } else if (pose.yaw < 2.5) {
                        const hint = 'Quay phải nhiều hơn...';
                        if (lastWarningMessage !== hint) {
                            lastWarningMessage = hint;
                            try {
                                await livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', 4, `↪️ ${hint}`);
                            } catch (e) { }
                        }
                    }
                }
            } catch (error) {
                console.error('❌ Lỗi trong liveness detection (register):', error);
            }
        }, 300);

        console.log('✅ startLivenessDetectionRegister started');
    } catch (error) {
        console.error('❌ Lỗi khởi động liveness detection (register):', error);
    }
};

window.stopLivenessDetection = () => {
    try {
        if (window._livenessRafId) {
            try {
                cancelAnimationFrame(window._livenessRafId);
                console.log('✅ canceled liveness RAF');
            } catch (e) {
                console.warn('cancelAnimationFrame error', e);
            }
            window._livenessRafId = null;
        }

        if (livenessDetectionInterval) {
            try {
                clearInterval(livenessDetectionInterval);
                console.log('✅ cleared global liveness interval');
            } catch (e) {
                console.warn('clearInterval error', e);
            }
            livenessDetectionInterval = null;
        }
        if (window._livenessIntervalId) {
            try {
                clearInterval(window._livenessIntervalId);
                console.log('✅ cleared global liveness interval (window._livenessIntervalId)');
            } catch (e) {
                console.warn('clearInterval error', e);
            }
            window._livenessIntervalId = null;
        }

        // ✅ XÓA MŨI TÊN KHI DỪNG
        try {
            const videos = document.querySelectorAll('video');
            videos.forEach(v => {
                try {
                    hideDirectionArrow(v);
                } catch (e) {
                    console.warn('Error hiding arrow for video', e);
                }
            });
        } catch (e) {
            console.warn('Error clearing arrows', e);
        }

        window._livenessRunning = false;
        livenessStep = 0;
        lastWarningMessage = '';
        lastWarningTimestamp = 0;
        randomActions = [];
        currentActionIndex = 0;

        try {
            window._livenessDotNetRef = null;
            if (livenessBlazorRef) {
                livenessBlazorRef = null;
                console.log('✅ cleared livenessBlazorRef (JS)');
            }
        } catch (e) {
            console.warn('Error clearing liveness refs', e);
        }

        console.log('✅ stopLivenessDetection: all cleared (JS)');
    } catch (error) {
        console.error('❌ stopLivenessDetection error:', error);
    }
};

window.captureImageDataUrl = (videoElementId, maxWidth = 1280, maxHeight = 1280) => {
    try {
        const video = document.getElementById(videoElementId);
        if (!video) {
            console.warn('captureImageDataUrl: video element not found:', videoElementId);
            return null;
        }

        let srcW = video.videoWidth || video.naturalWidth || 0;
        let srcH = video.videoHeight || video.naturalHeight || 0;

        if (!srcW || !srcH) {
            const rect = video.getBoundingClientRect();
            const dpr = window.devicePixelRatio || 1;
            srcW = Math.round(rect.width * dpr) || 640;
            srcH = Math.round(rect.height * dpr) || 480;
        }

        if (!srcW || !srcH) {
            console.warn('captureImageDataUrl: cannot determine video dimensions');
            return null;
        }

        const scale = Math.min(1, Math.min(maxWidth / srcW, maxHeight / srcH));
        const canvasW = Math.max(1, Math.round(srcW * scale));
        const canvasH = Math.max(1, Math.round(srcH * scale));

        const canvas = document.createElement("canvas");
        canvas.width = canvasW;
        canvas.height = canvasH;
        const ctx = canvas.getContext("2d");

        let isMirrored = false;
        try {
            const cs = window.getComputedStyle(video);
            const transform = cs && (cs.transform || cs.webkitTransform);
            if (transform && transform !== 'none') {
                isMirrored = transform.includes('matrix(-1') || transform.includes('scaleX(-1)');
            }
        } catch (e) {
            // ignore
        }

        if (isMirrored) {
            ctx.translate(canvas.width, 0);
            ctx.scale(-1, 1);
        }

        try {
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        } catch (e) {
            try {
                ctx.drawImage(video, 0, 0);
            } catch (err) {
                console.error('captureImageDataUrl: drawImage failed', err);
                return null;
            }
        }

        const dataUrl = canvas.toDataURL("image/jpeg", 0.85);
        console.log('📷 captureImageDataUrl: thành công', canvasW + 'x' + canvasH);
        return dataUrl;
    } catch (e) {
        console.error('captureImageDataUrl error:', e);
        return null;
    }
};

// ===== HÀM TẠO VÀ QUẢN LÝ MŨI TÊN CHỈ DẪN =====
function showDirectionArrow(video, direction) {
    try {
        if (!video) return;

        // Tìm wrapper
        let wrapper = video.closest ? video.closest('.video-wrapper') : null;
        if (!wrapper) wrapper = video.parentElement?.parentElement || video.parentElement || document.body;

        // Xóa mũi tên cũ nếu có
        const oldArrow = wrapper.querySelector('.direction-arrow-overlay');
        if (oldArrow) {
            oldArrow.remove();
        }

        // Nếu direction là null hoặc không phải look_left/look_right thì không hiện mũi tên
        if (!direction || (direction !== 'look_left' && direction !== 'look_right')) {
            return;
        }

        // Tạo SVG overlay cho mũi tên
        const arrowSvg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        arrowSvg.setAttribute('class', 'direction-arrow-overlay');
        arrowSvg.style.position = 'absolute';
        arrowSvg.style.top = '50%';
        arrowSvg.style.transform = 'translateY(-50%)';
        arrowSvg.style.width = '80px';
        arrowSvg.style.height = '80px';
        arrowSvg.style.pointerEvents = 'none';
        arrowSvg.style.zIndex = '15';
        arrowSvg.style.filter = 'drop-shadow(0 2px 4px rgba(0,0,0,0.3))';
        arrowSvg.style.opacity = '0';
        arrowSvg.style.transition = 'opacity 300ms ease-in-out, left 300ms ease-in-out, right 300ms ease-in-out';

        // Vị trí và hướng mũi tên
        if (direction === 'look_left') {
            arrowSvg.style.left = '10%';
            // Mũi tên chỉ sang TRÁI
            arrowSvg.innerHTML = `
                <defs>
                    <linearGradient id="arrowGradientLeft" x1="0%" y1="0%" x2="100%" y2="0%">
                        <stop offset="0%" style="stop-color:#4CAF50;stop-opacity:1" />
                        <stop offset="100%" style="stop-color:#81C784;stop-opacity:1" />
                    </linearGradient>
                </defs>
                <path d="M 60 15 L 30 40 L 60 65 M 30 40 L 70 40" 
                      stroke="url(#arrowGradientLeft)" 
                      stroke-width="6" 
                      stroke-linecap="round" 
                      stroke-linejoin="round" 
                      fill="none">
                    <animate attributeName="stroke-dasharray" 
                             from="0,200" 
                             to="200,0" 
                             dur="1s" 
                             repeatCount="indefinite"/>
                </path>
                <animateTransform attributeName="transform"
                                  type="translate"
                                  values="10,0; 0,0; 10,0"
                                  dur="1.5s"
                                  repeatCount="indefinite"/>
            `;
        } else if (direction === 'look_right') {
            arrowSvg.style.right = '10%';
            // Mũi tên chỉ sang PHẢI
            arrowSvg.innerHTML = `
                <defs>
                    <linearGradient id="arrowGradientRight" x1="0%" y1="0%" x2="100%" y2="0%">
                        <stop offset="0%" style="stop-color:#2196F3;stop-opacity:1" />
                        <stop offset="100%" style="stop-color:#64B5F6;stop-opacity:1" />
                    </linearGradient>
                </defs>
                <path d="M 20 15 L 50 40 L 20 65 M 50 40 L 10 40" 
                      stroke="url(#arrowGradientRight)" 
                      stroke-width="6" 
                      stroke-linecap="round" 
                      stroke-linejoin="round" 
                      fill="none">
                    <animate attributeName="stroke-dasharray" 
                             from="0,200" 
                             to="200,0" 
                             dur="1s" 
                             repeatCount="indefinite"/>
                </path>
                <animateTransform attributeName="transform"
                                  type="translate"
                                  values="-10,0; 0,0; -10,0"
                                  dur="1.5s"
                                  repeatCount="indefinite"/>
            `;
        }

        // Thêm vào wrapper
        wrapper.appendChild(arrowSvg);

        // Fade in animation
        setTimeout(() => {
            arrowSvg.style.opacity = '1';
        }, 50);

        console.log(`➡️ Hiển thị mũi tên: ${direction}`);
    } catch (e) {
        console.warn('showDirectionArrow error', e);
    }
}

// Hàm xóa mũi tên
function hideDirectionArrow(video) {
    try {
        if (!video) return;

        let wrapper = video.closest ? video.closest('.video-wrapper') : null;
        if (!wrapper) wrapper = video.parentElement?.parentElement || video.parentElement || document.body;

        const arrow = wrapper.querySelector('.direction-arrow-overlay');
        if (arrow) {
            arrow.style.opacity = '0';
            setTimeout(() => {
                arrow.remove();
            }, 300);
            console.log('🚫 Đã ẩn mũi tên');
        }
    } catch (e) {
        console.warn('hideDirectionArrow error', e);
    }
}