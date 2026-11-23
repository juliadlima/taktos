# Implantação e automação

## Executar localmente com Docker

Em um ambiente Docker compatível com o SQL Server para Linux, copie `.env.example` para `.env` e preencha as duas senhas. O serviço SQL mantém seus arquivos em volume persistente. A etapa de preparação cria o banco, aplica os scripts e configura o usuário SQL da aplicação. A aplicação web é publicada com .NET 10.

```bash
docker compose up -d --build
docker compose exec -T portal dotnet Taktos.API.dll preparar-contas
```

Consulte `http://localhost:5097/health/db`. Para desligar o ambiente e manter o banco, execute `docker compose down`.

## Publicar em uma VM Linux

1. Configure Docker e o plugin Compose na VM. Aponte o DNS do domínio escolhido para a VM e disponibilize as portas HTTP e HTTPS para o Caddy. A porta da aplicação continua vinculada ao loopback da VM.
2. Copie o projeto para `/opt/taktos` e configure `.env` com as senhas, `TAKTOS_DOMINIO` e `TAKTOS_IMAGE` se usar a imagem do GHCR. Autentique o Docker na VM se o registro for privado.
3. Para construir na VM, execute `docker compose -f compose.yaml -f infra/compose.nuvem.yaml up -d --build`. Para uma imagem publicada, use os mesmos arquivos com `pull portal` e depois `up -d --no-build`.
4. Para demonstração com dados fictícios, execute `docker compose -f compose.yaml -f infra/compose.nuvem.yaml exec -T -e ASPNETCORE_ENVIRONMENT=Development portal dotnet Taktos.API.dll preparar-contas`. O comando usa Development só para preparar as contas. O processo HTTP permanece em Production e emite cookies seguros.
5. Verifique `https://seu-dominio/health`, `https://seu-dominio/health/db` e as telas do portal.

## Atualizar uma instalação Docker

Depois de copiar a nova versão do projeto para a pasta de implantação, rode a etapa de banco e atualize o contêiner do portal. Na VM, pela pasta `/opt/taktos`:

```bash
docker compose -f compose.yaml -f infra/compose.nuvem.yaml run --rm preparar-banco
docker compose -f compose.yaml -f infra/compose.nuvem.yaml up -d --build
```

A etapa de banco aplica as atualizações de cadastro, perfis, empresas, filtros, indicadores de OS e relatos de inclusão (scripts 05 a 12) e concede as permissões às procedures. Para uma imagem já publicada, atualize `TAKTOS_IMAGE` e use o fluxo de pull e reinício descrito acima.

## CI/CD e monitoramento

O workflow **Compilar e validar o Taktos** compila o host, sobe um SQL Server temporário e executa o fluxo integrado. Quando a validação de um push para `main` passa, o workflow publica a imagem no GHCR com as tags da revisão e de `main`.

O workflow **Implantar Taktos em VM** é acionado manualmente. Configure o ambiente `demonstracao` com `TAKTOS_VM_HOST`, `TAKTOS_VM_USER`, `TAKTOS_VM_CHAVE` e `TAKTOS_VM_HOST_KEY` (a linha verificada de known_hosts). Ele atualiza a imagem na VM e verifica a aplicação.

Use as rotas de saúde e os logs de `docker compose logs portal proxy` para diagnosticar falhas de aplicação e conectividade. O banco e as chaves de sessão usam volumes persistentes. A configuração entregue é de uma VM. Para escalar o host a múltiplas réplicas, planeje um armazenamento compartilhado das chaves de sessão e um banco acessível a todas as instâncias.
