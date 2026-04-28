namespace EduConnect.Api.DTOs;

public record AdminProfessorDetalheDto(
    int ProfessorId,
    string Matricula,
    string Nome,
    bool Ativo,
    List<AdminOfertaResumoDto> Ofertas
);
