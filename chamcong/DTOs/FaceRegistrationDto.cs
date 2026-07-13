namespace chamcong.DTOs;

public class FaceRegistrationDto
{
    public double[] FaceDescriptor { get; set; } = Array.Empty<double>();
    public int EmployeeId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

}