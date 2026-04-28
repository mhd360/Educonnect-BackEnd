namespace EduConnect.Api.DTOs;

public record AdminPessoaListItemDto(
    int Id,
    string Matricula,
    string Nome,
    string Cpf,
    bool Ativo
);
