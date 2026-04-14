
  Mục tiêu chính
	Hệ thống chấm công bằng nhận diện khuôn mặt + GPS.
	2 luồng chính: Attendance (chấm nhanh) và RegisterFace (đăng ký khuôn mặt chất lượng cao).
 Lưu lịch sử ngày giờ ngày tháng năm 
 giờ vào/ra
 ảnh chụp vào/ra lúc chấm công
 tổng số giờ làm việc
 GPS vào/ra
 Độ lệch vị trí từ GPS điện thoại tới địa chỉ công ty
 IP vào/ra
 Thiết bị vào/ra
  
	Công nghệ chính
	Backend: .NET 8, ASP.NET Core (Blazor Server Interactive).
	ORM/DB: Entity Framework Core → SQL Server.
	Frontend: Blazor Server (Razor components) + JavaScript interop (IJSRuntime / DotNetObjectReference).
	Face/ML client: face-api.js (vladmandic build) — tải model từ CDN.
	Browser APIs: getUserMedia, Canvas, SVG, CSS transforms.
	Dev / infra: Swagger, SignalR tuning (timeouts), HttpClient (ApiService).

BE
	wwwroot/js/face-recognition.js — toàn bộ logic camera, liveness, capture, descriptor, overlay, mũi tên hướng dẫn.
	Components/Pages/Attendance.razor — UI gọi startWebcam / startLivenessDetection (random liveness).
	Components/Pages/RegisterFace.razor — UI gọi startLivenessDetectionRegister (5 bước cố định).
	Services/ApiService.cs — gọi API server để lưu ảnh/descriptor/chấm công.
	Data/ApplicationDbContext.cs và Models — lưu Employee, AttendanceRecord, Face data.
	Program.cs — cấu hình Blazor, SignalR, JSON, HttpClient.

	Chức năng chính & cơ chế hoạt động
	Bật camera: startWebcam(videoId) sử dụng getUserMedia, mirror bằng CSS scaleX(-1).
	Load model: loadFaceModels() tải tinyFaceDetector, landmark, recognition; ssdMobilenetv1 làm fallback.
	Liveness:
	Attendance: generateRandomActions() → chọn 2/4 hành động ngẫu nhiên (look_left, look_right, move_close, move_far).
	Register: 5 bước cố định (nhìn thẳng → lại gần → lùi xa → quay trái → quay phải).
	Vòng lặp detect (setInterval ~300ms): detectSingleFace + withFaceLandmarks → tính box, faceSize, head pose (yaw).
	calculateHeadPose() ước lượng yaw dựa trên mũi & trung tâm 2 mắt (cân chỉnh mirror).
	State machine chuyển bước, gợi hint và invoke về Blazor bằng livenessBlazorRef.invokeMethodAsync('UpdateLivenessStep', ...).
	Overlay & hướng dẫn:
	SVG overlay (.face-guide-frame) được resize động theo action/step (setOverlaySizeForAction / setOverlaySizeForRegisterStep).
	Mũi tên chỉ hướng xuất hiện khi yêu cầu quay trái/phải (showDirectionArrow / hideDirectionArrow).
	Capture descriptor / ảnh:
	captureFaceDescriptor() dùng face-api .withFaceDescriptor(), chuẩn hoá (L2 norm) rồi trả về descriptor + score.
	captureImageDataUrl() vẽ video lên canvas, xử lý mirror, xuất JPEG base64.
	So sánh:
	compareFaceDescriptors() chuẩn hoá, tính dot product → similarity, isMatch theo threshold cosine (0.70).
	Server flow:
	Blazor nhận descriptor/ảnh → ApiService gửi tới controller → server lưu/so sánh bằng EF/logic server.
	Lifecycle & cleanup:
	stopWebcam(), stopLivenessDetection() dọn tracks, clearInterval, remove overlays/arrows, reset state.
	Tunables / lưu ý vận hành
	Ngưỡng: yaw thresholds, faceSize ranges, inputSize và scoreThreshold trong TinyFaceDetector có thể điều chỉnh.
	Model loading: cần CDN reachable; nếu chậm cân nhắc phục vụ models local.
	Mirror: video thường mirror; calculateHeadPose đã bù nếu transform có scaleX(-1).
	CSS: nếu overlay không phản hồi, kiểm tra selector .face-guide-frame hoặc CSS override.
	Blazor Server: đã tăng JSInteropDefaultCallTimeout và SignalR settings trong Program.cs — tránh timeout khi xử lý lâu.
