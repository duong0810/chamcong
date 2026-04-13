namespace chamcong.Constants;

public static class AppConstants
{
    // API Endpoints
    public const string ApiBaseUrl = "/api";
    public const string EmployeeEndpoint = "/api/employee";
    public const string AttendanceEndpoint = "/api/attendance";

    // Face Recognition
    public const double FaceMatchThreshold = 0.6; // 60% similarity
    public const int FaceDescriptorSize = 128; // Face-API.js default

    // Attendance Rules
    public const int MaxCheckInsPerDay = 1;
    public const int MaxCheckOutsPerDay = 1;

    // Messages
    public const string CheckInSuccessMessage = "Chấm công vào thành công!";
    public const string CheckOutSuccessMessage = "Chấm công ra thành công!";
    public const string AlreadyCheckedInMessage = "Bạn đã chấm công vào hôm nay!";
    public const string NotCheckedInMessage = "Bạn chưa chấm công vào!";
    public const string FaceNotRecognizedMessage = "Không nhận diện được khuôn mặt!";
}