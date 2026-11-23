# Taktos

Portal B2B para gestão de ordens de serviço e acompanhamento de entregas de software. A fornecedora organiza a triagem no Kanban. A TI do cliente abre e acompanha as OSs da sua empresa, recebe versões com GMUD e registra o resultado da homologação.

## Estrutura

| Projeto | Responsabilidade |
|---|---|
| Taktos.API | Aplicação ASP.NET Core com controllers REST, autenticação por cookie e composição do portal. |
| Taktos.Domain | Modelos e regras do domínio, compartilhados com a aplicação móvel. |
| Taktos.Infrastructure | Repositórios e persistência com SQL Server. |
| Taktos.Web | Controllers e views Razor MVC, JavaScript e CSS. O host ASP.NET Core serve as views e a API no mesmo endereço. |
| Taktos.Mobile | Projeto .NET MAUI para Android com login, listagem e detalhe das OSs. |

Abra **Taktos.sln** para ver os cinco projetos. O filtro **Taktos.Local.slnf** abre os quatro projetos necessários para executar a API e o MVC sem o workload Android.

## Executar no Windows

1. Extraia o ZIP em uma pasta, por exemplo `C:\Projetos\Taktos`. Abra um PowerShell na pasta que contém `Taktos.sln`.
2. No SSMS, conecte ao mesmo **SQL Server Express** que você utilizou antes. Em um banco novo, execute os scripts de `banco` na ordem **01, 02, 03, 05, 06, 07, 08, 09, 10, 11 e 12**. Se já executou o script 11, aplique somente `banco/12-relatos-de-inclusao.sql`. Para versões anteriores, execute os scripts que faltam, em ordem. A atualização mantém as empresas, contas e OSs existentes.
3. O projeto usa o mesmo identificador de segredos e a mesma chave de conexão das sprints anteriores. Se `/health/db` indicar indisponibilidade, confira a instância e configure a conexão abaixo com o mesmo nome de servidor usado no SSMS.

```powershell
dotnet user-secrets set "ConnectionStrings:PortalEntregas" "Server=noteJulia\SQLEXPRESS;Database=PortalEntregas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5" --project ".\Taktos.API\Taktos.API.csproj"
```

4. Prepare as contas fictícias. O comando mostra no terminal a senha de cada conta criada. As contas já criadas na S1.03 continuam com as senhas anteriores.

```powershell
dotnet run --project ".\Taktos.API\Taktos.API.csproj" --launch-profile Taktos.Local -- preparar-contas
```

Se perdeu uma senha de demonstração, rode o comando com `-- preparar-contas --redefinir`. Ele redefine somente as três contas fictícias conhecidas e mostra as novas senhas. Guarde essas senhas no seu ambiente local.

5. No Visual Studio, abra `Taktos.Local.slnf`, defina **Taktos.API** como projeto de inicialização e rode pelo perfil **Taktos.Local**. Ou execute:

```powershell
dotnet run --project ".\Taktos.API\Taktos.API.csproj" --launch-profile Taktos.Local
```

Abra [http://localhost:5097](http://localhost:5097). Com a sessão ativa, a marca **Taktos** e o endereço inicial levam a **Ordens de serviço**. Confira também [a API](http://localhost:5097/health) e [a conexão com o banco](http://localhost:5097/health/db).

| Conta | Perfil |
|---|---|
| `fornecedora@portal.test` | Equipe fornecedora |
| `ti.alfa@portal.test` | TI do Cliente Alfa |
| `ti.beta@portal.test` | TI do Cliente Beta |

## Cadastrar empresas clientes

1. Entre como equipe fornecedora e abra **Empresas** no menu de ordens.
2. Preencha **Nome da empresa** e clique em **Cadastrar empresa**. O Taktos vincula o cliente à sua fornecedora e gera um código para o cadastro dos usuários.
3. Na empresa cadastrada, use **Adicionar sistema** para incluir os sistemas para os quais o cliente poderá abrir OSs, por exemplo, **REDEX**.
4. Compartilhe o nome exato ou o código com a equipe cliente para que ela solicite acesso.

Em **Editar empresa**, é possível mudar o nome mantendo o código. **Desativar empresa** suspende o acesso de todos os seus usuários e a aprovação de novas contas. A fornecedora continua com acesso às OSs e ao histórico. **Reativar empresa** libera novos logins para as contas que estiverem ativas.

## Criar uma conta pelo portal

1. Na tela de login, selecione **Criar conta** e informe seu nome, e-mail, senha e o nome de uma empresa cliente já cadastrada no Taktos ou o código informado pela fornecedora. Use o código se houver empresas com o mesmo nome.
2. A solicitação fica pendente. Na conta da fornecedora, abra **Aprovações de cadastro**. Confira o nome, o e-mail e a empresa e selecione **Aprovar acesso** ou **Recusar**.
3. Após a aprovação, entre com o e-mail e a senha cadastrados. Em **Usuários da empresa**, a equipe cliente vê os nomes e e-mails dos usuários ativos da própria empresa. Em **Ordens de serviço**, vê as OSs da empresa, inclusive quando abertas por colegas.
4. Em **Meu perfil**, edite o nome, o e-mail ou a senha. Para mudar o e-mail ou a senha, confirme a senha atual. A empresa e o perfil de acesso são exibidos para consulta.

Em **Aprovações de cadastro**, a fornecedora também pode **Criar conta manualmente** em uma empresa ativa. Ela informa empresa, nome, e-mail e senha inicial. A conta nasce com acesso liberado. Em **Contas cadastradas**, pode editar nome e e-mail, redefinir a senha ou desativar e reativar uma conta individual. A desativação encerra as sessões dessa conta e preserva seus registros anteriores nas OSs.

Em uma instalação Docker já ativa, confira [as instruções de atualização](docs/implantacao.md#atualizar-uma-instalação-docker).

## Administrar a equipe fornecedora

Entre com a conta **Consultor ADM**. Na instalação de demonstração, o script 08 vincula essa permissão ao usuário existente com o e-mail `fornecedora@portal.test`. Para um banco novo, o comando **preparar-contas** cria e vincula a conta. Depois de vinculada, a permissão acompanha o usuário caso o nome ou o e-mail do cadastro mude.

- Em **Equipe fornecedora**, crie uma conta interna com nome, e-mail e senha inicial. É possível editar os dados, redefinir a senha, desativar e reativar os demais usuários da equipe.
- Em **Meu perfil**, use **Identidade da fornecedora** para alterar o nome da própria empresa fornecedora e o nome exibido do perfil de toda a equipe. As permissões e os vínculos existentes continuam associados ao cadastro original.

## Filtrar ordens de serviço por pessoa

Abra **Solicitante** para filtrar pelas pessoas que abriram as OSs. Abra **Responsável** para filtrar pelas pessoas designadas para atendê-las. Marque uma ou mais pessoas em cada grupo. Dentro de um grupo, basta a OS corresponder a uma das pessoas selecionadas. Ao usar os dois grupos, a OS deve corresponder a ambos. Os filtros podem ser combinados com **Sistema**, **Situação** e **Previsão**.

## Indicadores das ordens de serviço

Os cartões de **OSs abertas sem previsão**, **Primeiras entregas no prazo** e **Versões devolvidas** mostram contagens. Na conta da fornecedora, abra **Indicadores**, abaixo de **Aprovações de cadastro**, para cadastrar um cartão. Informe o nome e selecione o que ele deve contar, por exemplo, **OSs em execução**. Em **Indicadores cadastrados**, os cartões padrão e personalizados aparecem com **Editar**. É possível mudar o nome e o critério de contagem. Os cartões aparecem em **Ordens de serviço** e são recalculados com os filtros e as permissões de cada usuário.

## Navegação do portal

A barra lateral está disponível em todas as telas internas. O menu da página atual abre seus submenus, que levam diretamente às seções. Em **Aprovações de cadastro**, use **Solicitações pendentes**, **Criar conta manualmente** ou **Contas cadastradas**. Em **Equipe fornecedora**, use **Novo usuário**, **Usuários da fornecedora** ou **Consultor ADM**, conforme as permissões de acesso.

## Aplicativo Android

No Visual Studio, instale o workload de desenvolvimento .NET MAUI com suporte ao **.NET 8 / Android** para abrir e executar **Taktos.Mobile** no emulador. Na pasta da solução, com o SDK e as ferramentas Android configurados, também é possível restaurar e compilar o projeto pelo terminal.

```powershell
dotnet workload restore ".\Taktos.Mobile\Taktos.Mobile.csproj"
dotnet build ".\Taktos.Mobile\Taktos.Mobile.csproj" -c Debug
```

Com a API rodando no computador, use `http://10.0.2.2:5097` no emulador Android. Para testar no celular por USB em **Debug**, execute `adb -d reverse tcp:5097 tcp:5097` no computador e informe `http://127.0.0.1:5097` no aplicativo. Para uma instalação publicada, informe seu endereço HTTPS. As telas do aplicativo permitem entrar, consultar OSs e detalhes de versões, atualizar a consulta e sair. A atualização lê os dados atuais da mesma API. As telas nativas de login, lista e detalhe usam a paleta clara, a identidade e a imagem de acesso do portal web.

## Demonstração e implantação

- [Roteiro de verificação das quatro sprints](docs/demonstracao.md)
- [Arquitetura, modelo de dados e rotas](docs/arquitetura.md)
- [Docker, pipeline e publicação](docs/implantacao.md)
- [Guia de uso disponível no portal](http://localhost:5097/guia.html)
- [Etapa 3 do PIM e roteiro de avaliação de acessibilidade](docs/acessibilidade.md)

O alvo local da solução web é .NET 8 para o Visual Studio 2022. O build em contêiner usa .NET 10. Para usar um workload mais recente no projeto Android, as propriedades `MobileFramework` e `PortalFramework` permitem apontar a compilação para `net10.0-android` e `net10.0` com o SDK correspondente.
