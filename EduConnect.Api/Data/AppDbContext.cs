using EduConnect.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Professor> Professores => Set<Professor>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<TurmaAluno> TurmaAlunos => Set<TurmaAluno>();
    public DbSet<TurmaProfessor> TurmaProfessores => Set<TurmaProfessor>();
    public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
    public DbSet<OfertaDisciplina> OfertaDisciplinas => Set<OfertaDisciplina>();
    public DbSet<OfertaAluno> OfertaAlunos => Set<OfertaAluno>();
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();
    public DbSet<TarefaResposta> TarefaRespostas => Set<TarefaResposta>();
    public DbSet<TarefaCorrecao> TarefaCorrecoes => Set<TarefaCorrecao>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<FaltaOfertaAluno> FaltaOfertaAlunos => Set<FaltaOfertaAluno>();
    public DbSet<OfertaFalta> OfertaFaltas => Set<OfertaFalta>();
    public DbSet<OfertaAlunoFalta> OfertaAlunoFaltas => Set<OfertaAlunoFalta>();
    public DbSet<OfertaNota> OfertaNotas => Set<OfertaNota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(u => u.Cpf).HasMaxLength(11).IsUnicode(false);
            e.HasIndex(u => u.Cpf).IsUnique();

            e.Property(x => x.Perfil).HasConversion<string>().HasMaxLength(20).IsRequired();

            e.HasIndex(x => x.Perfil)
             .IsUnique()
             .HasFilter("[Perfil] = 'ADMIN'");
        });

        modelBuilder.Entity<Aluno>(e =>
        {
            e.Property(x => x.Matricula).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Matricula).IsUnique();

            e.HasOne(x => x.Usuario)
             .WithOne(u => u.Aluno)
             .HasForeignKey<Aluno>(x => x.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Professor>(e =>
        {
            e.Property(x => x.Matricula).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Matricula).IsUnique();

            e.HasOne(x => x.Usuario)
             .WithOne(u => u.Professor)
             .HasForeignKey<Professor>(x => x.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Turma>(e =>
        {
            e.Property(x => x.Nome).HasMaxLength(10).IsRequired();

            e.Property(x => x.Ano).IsRequired();
            e.Property(x => x.Semestre).IsRequired();

            e.Property(x => x.Periodo)
                .HasConversion<string>()
                .HasMaxLength(15)
                .IsRequired();

            // garante que não exista duplicidade de coorte
            e.HasIndex(x => new { x.Ano, x.Semestre, x.Periodo }).IsUnique();

            // opcional: também garante que o código seja único
            e.HasIndex(x => x.Nome).IsUnique();
        });

        modelBuilder.Entity<TurmaAluno>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Turma)
                .WithMany()
                .HasForeignKey(x => x.TurmaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Aluno)
                .WithMany()
                .HasForeignKey(x => x.AlunoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um aluno não pode estar ativo na mesma turma 2x
            e.HasIndex(x => new { x.TurmaId, x.AlunoId })
                .IsUnique()
                .HasFilter("[Ativo] = 1");
        });

        modelBuilder.Entity<TurmaProfessor>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Turma)
                .WithMany()
                .HasForeignKey(x => x.TurmaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Professor)
                .WithMany()
                .HasForeignKey(x => x.ProfessorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um professor não pode estar ativo na mesma turma 2x
            e.HasIndex(x => new { x.TurmaId, x.ProfessorId })
                .IsUnique()
                .HasFilter("[Ativo] = 1");
        });

        modelBuilder.Entity<Disciplina>(e =>
        {
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.CargaHoraria).IsRequired();
            e.Property(x => x.TotalAulas).HasDefaultValue(16);
            e.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<OfertaDisciplina>(e =>
        {
            e.Property(x => x.Ano).IsRequired();
            e.Property(x => x.Semestre).IsRequired();

            e.Property(x => x.Periodo)
                .HasConversion<string>()
                .HasMaxLength(15)
                .IsRequired();

            e.Property(x => x.TotalAulas).IsRequired().HasDefaultValue(16);

            e.HasOne(x => x.Disciplina)
                .WithMany()
                .HasForeignKey(x => x.DisciplinaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Professor)
                .WithMany()
                .HasForeignKey(x => x.ProfessorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Turma)
                .WithMany()
                .HasForeignKey(x => x.TurmaId)
                .OnDelete(DeleteBehavior.Restrict);

            // evita duplicar a mesma oferta (considerando TurmaId)
            e.HasIndex(x => new { x.DisciplinaId, x.ProfessorId, x.Ano, x.Semestre, x.Periodo, x.TurmaId }).IsUnique();
        });

        modelBuilder.Entity<OfertaAluno>(e =>
        {
            e.HasOne(x => x.OfertaDisciplina)
                .WithMany()
                .HasForeignKey(x => x.OfertaDisciplinaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Aluno)
                .WithMany()
                .HasForeignKey(x => x.AlunoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.AlunoId })
                .IsUnique()
                .HasFilter("[Ativo] = 1");

            e.Property(x => x.NotaExame)
                .HasPrecision(5, 2);

            e.Property(x => x.Faltas)
                .HasDefaultValue(0);
        });

        modelBuilder.Entity<Tarefa>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(500);

            e.Property(x => x.Peso).HasPrecision(6, 2).IsRequired();

            e.HasOne(x => x.OfertaDisciplina)
                .WithMany()
                .HasForeignKey(x => x.OfertaDisciplinaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.Titulo });
        });

        modelBuilder.Entity<TarefaResposta>(e =>
        {
            e.Property(x => x.Conteudo).HasMaxLength(4000).IsRequired();

            e.HasOne(x => x.Tarefa)
                .WithMany()
                .HasForeignKey(x => x.TarefaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Aluno)
                .WithMany()
                .HasForeignKey(x => x.AlunoId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1 resposta por aluno por tarefa (sem reentrega, mesmo se Ativa=false)
            e.HasIndex(x => new { x.TarefaId, x.AlunoId })
                .IsUnique();
        });

        modelBuilder.Entity<TarefaCorrecao>(e =>
        {
            e.Property(x => x.Nota).HasPrecision(6, 2).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(2000);

            e.HasOne(x => x.TarefaResposta)
                .WithMany()
                .HasForeignKey(x => x.TarefaRespostaId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1 correção ativa por resposta
            e.HasIndex(x => x.TarefaRespostaId)
                .IsUnique()
                .HasFilter("[Ativa] = 1");
        });

        modelBuilder.Entity<Evento>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(1000);

            e.Property(x => x.Data).HasColumnType("date").IsRequired();
            e.Property(x => x.DiaInteiro).IsRequired();

            // time(0) => sem segundos (só HH:mm)
            e.Property(x => x.HoraInicio).HasColumnType("time(0)");
            e.Property(x => x.HoraFim).HasColumnType("time(0)");

            e.HasOne(x => x.OfertaDisciplina)
                .WithMany()
                .HasForeignKey(x => x.OfertaDisciplinaId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.Data });
        });

        modelBuilder.Entity<FaltaOfertaAluno>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.AlunoId, x.NumeroAula })
             .IsUnique();

            e.Property(x => x.NumeroAula).IsRequired();

            e.HasOne(x => x.OfertaDisciplina)
             .WithMany()
             .HasForeignKey(x => x.OfertaDisciplinaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Aluno)
             .WithMany()
             .HasForeignKey(x => x.AlunoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OfertaFalta>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.AulaNumero)
             .IsRequired();

            e.Property(x => x.Ativa)
             .HasDefaultValue(true);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.AlunoId, x.AulaNumero })
             .IsUnique();

            e.HasOne(x => x.OfertaDisciplina)
             .WithMany()
             .HasForeignKey(x => x.OfertaDisciplinaId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Aluno)
             .WithMany()
             .HasForeignKey(x => x.AlunoId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OfertaAlunoFalta>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasIndex(x => new { x.OfertaAlunoId, x.NumeroAula })
             .IsUnique();

            e.HasOne(x => x.OfertaAluno)
             .WithMany()
             .HasForeignKey(x => x.OfertaAlunoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(x => x.NumeroAula).IsRequired();
        });

        modelBuilder.Entity<OfertaNota>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasIndex(x => new { x.OfertaDisciplinaId, x.AlunoId })
             .IsUnique();

            e.Property(x => x.A1).HasPrecision(5, 2);
            e.Property(x => x.A2).HasPrecision(5, 2);
            e.Property(x => x.A3).HasPrecision(5, 2);

            e.Property(x => x.AtualizadoEm).IsRequired();

            e.HasOne(x => x.OfertaDisciplina)
             .WithMany()
             .HasForeignKey(x => x.OfertaDisciplinaId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Aluno)
             .WithMany()
             .HasForeignKey(x => x.AlunoId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
