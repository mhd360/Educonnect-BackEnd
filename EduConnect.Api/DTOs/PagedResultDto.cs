namespace EduConnect.Api.DTOs;

public record PagedResultDto<T>(int Total, List<T> Items);
