# EduConnect

## Sobre o projeto

O **EduConnect** é uma plataforma acadêmica que desenvolvi com a proposta de centralizar, em um único sistema, as principais rotinas de **alunos**, **professores** e **administradores**.

A ideia do projeto surgiu da necessidade de organizar melhor processos acadêmicos que normalmente ficam espalhados em diferentes ferramentas, como acompanhamento de notas, atividades, frequência, calendário e gestão de usuários. Com isso, o objetivo foi criar uma aplicação clara, funcional e com uma navegação simples, onde cada perfil tem acesso apenas ao que realmente precisa.

Ao longo do desenvolvimento, fui estruturando o sistema por módulos, começando pelas funcionalidades principais e evoluindo gradualmente até chegar em uma plataforma com fluxos bem definidos para cada tipo de usuário.

---

## Objetivo

O principal objetivo do EduConnect é facilitar a gestão acadêmica por meio de uma interface web intuitiva, organizada por perfil de acesso.

De forma mais prática, o sistema foi pensado para:

- permitir que o **aluno** acompanhe sua vida acadêmica;
- permitir que o **professor** gerencie notas, atividades, frequência e eventos;
- permitir que o **administrador** mantenha a estrutura acadêmica organizada, cuidando de usuários, disciplinas, e ofertas.

---

## Perfis do sistema

### Aluno

No painel do aluno, foquei em entregar uma experiência simples e objetiva. O estudante consegue acessar rapidamente as informações mais importantes da sua rotina acadêmica.

Funcionalidades principais:
- visualização do dashboard inicial;
- consulta de notas;
- acompanhamento de atividades;
- visualização do calendário;
- alteração de senha e saída pelo menu de perfil.

---

### Professor

No painel do professor, a proposta foi ampliar as possibilidades de gestão acadêmica sem perder o padrão visual do sistema.

Funcionalidades principais:
- dashboard com visão geral;
- lançamento e edição de notas;
- gerenciamento de atividades;
- controle de frequência;
- gerenciamento de calendário e eventos;
- alteração de senha e saída pelo menu de perfil.

---

### Administrador

No perfil de administrador, concentrei as funcionalidades voltadas para organização e manutenção do sistema.

Funcionalidades principais:
- consulta de usuários;
- cadastro de novos usuários;
- edição de informações básicas de usuários;
- ativação e desativação de usuários;
- gestão de disciplinas;
- associação e desassociação de professores, turmas e ofertas;
- controle de eventos institucionais.

---

## Estrutura do projeto

O projeto foi organizado separando páginas, seções, estilos e serviços, para facilitar manutenção e evolução.

De forma geral, a estrutura segue esta ideia:

```text
/pages
  aluno.html
  professor.html
  admin.html
  /sections
    /aluno
    /professor
    /admin

/js
  aluno-page.js
  professor-page.js
  admin-page.js
  /services
    alunoService.js
    professorService.js
    adminService.js

/styles
  reset.css
  typography.css
  globalStyles.css
  aluno.css
  professor.css
  admin.css