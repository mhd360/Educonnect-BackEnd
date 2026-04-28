namespace EduConnect.Api.Domain;

public class Evento
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public string Titulo { get; set; } = null!;
    public string? Descricao { get; set; }

    public DateOnly Data { get; set; }          // dia/mês/ano
    public bool DiaInteiro { get; set; }        // se true, não usa horas

    public TimeOnly? HoraInicio { get; set; }   // hora:minuto
    public TimeOnly? HoraFim { get; set; }      // hora:minuto

    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
