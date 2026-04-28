namespace EduConnect.Api.DTOs;

public record AdminAlunoDetalheDto(
    int AlunoId,
    string Matricula,
    string Nome,
    bool Ativo,
    List<AdminOfertaResumoDto> Ofertas
);
