namespace EduConnect.Api.DTOs;

public record AdminResumoDto(
    int TotalUsuarios,
    int TotalAlunos,
    int TotalProfessores,
    int TotalTurmas,
    int TotalDisciplinas,
    int TotalOfertas,
    int TotalTarefasAtivas,
    int TotalRespostasAtivas,
    int TotalRespostasPendentesCorrecao
);
