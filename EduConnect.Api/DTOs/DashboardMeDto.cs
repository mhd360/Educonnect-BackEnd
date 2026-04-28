namespace EduConnect.Api.DTOs;

public record DashboardMeDto(
    string Perfil,
    int UsuarioId,
    string Nome,
    string? Matricula,

    List<ProximoEventoDto> ProximosEventos,

    List<TarefaPendenteDto>? TarefasPendentes,
    List<TarefaCorrigidaDto>? TarefasCorrigidas,
    List<TarefaParaCorrigirDto>? TarefasParaCorrigir
);
