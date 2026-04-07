namespace OrduCep.Application.DTOs;

public class CalendarDayDto
{
    public DateTime Date { get; set; }
    public bool IsAvailable { get; set; }
    public int AvailableSlotsCount { get; set; }
}
