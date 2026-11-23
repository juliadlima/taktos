import assert from "node:assert/strict";

const base = process.env.TAKTOS_URL ?? "http://localhost:5097";

async function chamar(rota, { cookie, corpo, metodo = "GET", esperado = 200 } = {}) {
    const headers = {};
    if (cookie) headers.cookie = cookie;
    if (corpo !== undefined) headers["Content-Type"] = "application/json";
    const resposta = await fetch(base + rota, {
        method: metodo, headers, body: corpo === undefined ? undefined : JSON.stringify(corpo)
    });
    const bruto = await resposta.text();
    assert.equal(resposta.status, esperado, metodo + " " + rota + " " + bruto);
    return {
        dados: bruto ? JSON.parse(bruto) : null,
        cookie: resposta.headers.get("set-cookie")?.split(";")[0]
    };
}

function abrirPagina(rota, cookie) {
    return fetch(base + rota, { headers: cookie ? { cookie } : {}, redirect: "manual" });
}

async function conferirRedirecionamento(rota, destino, cookie) {
    const resposta = await abrirPagina(rota, cookie);
    assert.equal(resposta.status, 302, rota);
    const location = resposta.headers.get("location");
    assert(location, "Redirecionamento sem destino em " + rota);
    assert.equal(new URL(location, base).pathname, destino);
}

async function login(email, senha) {
    assert(senha, "Defina a senha de demonstração para " + email);
    const resposta = await chamar("/api/acesso/login", {
        metodo: "POST", corpo: { email, senha }, esperado: 204
    });
    assert(resposta.cookie, "Cookie de login ausente");
    return resposta.cookie;
}

assert.equal((await chamar("/health")).dados.status, "ok");
assert.equal((await chamar("/health/db")).dados.status, "ok");
await chamar("/api/ordens", { esperado: 401 });

const fornecedora = await login("fornecedora@portal.test", process.env.TAKTOS_TEST_FORNECEDORA);
const alfa = await login("ti.alfa@portal.test", process.env.TAKTOS_TEST_ALFA);
const beta = await login("ti.beta@portal.test", process.env.TAKTOS_TEST_BETA);

const inicioPublico = await abrirPagina("/");
assert.equal(inicioPublico.status, 200);
const htmlPublico = await inicioPublico.text();
assert.match(htmlPublico, /id="titulo-inicio"/);
assert.match(htmlPublico, /href="\/login\.html">Acessar o portal/);
assert.doesNotMatch(htmlPublico, /Consulta móvel/i);
await conferirRedirecionamento("/ordens.html", "/login.html");

for (const cookie of [fornecedora, alfa]) {
    await conferirRedirecionamento("/", "/ordens.html", cookie);
    await conferirRedirecionamento("/login.html", "/ordens.html", cookie);
    const ordensWeb = await abrirPagina("/ordens.html", cookie);
    assert.equal(ordensWeb.status, 200);
    const htmlOrdens = await ordensWeb.text();
    assert.match(htmlOrdens, /id="form-nova"/);
    assert.match(htmlOrdens, /id="opcoes-solicitante"/);
    assert.match(htmlOrdens, /id="opcoes-responsavel"/);
    assert.match(htmlOrdens, /class="marca" href="\/"/);
    assert.doesNotMatch(htmlOrdens, /Consulta móvel/i);
}

for (const [cookie, empresas] of [[fornecedora, ["Cliente Alfa", "Cliente Beta"]],
    [alfa, ["Cliente Alfa"]], [beta, ["Cliente Beta"]]]) {
    const { dados } = await chamar("/api/acesso/", { cookie });
    const nomes = dados.empresasVisiveis.map(empresa => empresa.nome);
    if (cookie === fornecedora)
        assert(empresas.every(nome => nomes.includes(nome)));
    else
        assert.deepEqual(nomes, empresas);
}

const sistemas = (await chamar("/api/ordens/opcoes", { cookie: alfa })).dados;
const redex = sistemas.find(sistema => sistema.nome === "REDEX");
assert(redex);
const criacao = await chamar("/api/ordens", {
    cookie: alfa, metodo: "POST", esperado: 201,
    corpo: { sistemaId: redex.id, tipo: "Correcao", titulo: "Correção do protocolo " + Date.now(),
        descricao: "Reproduzir a emissão do protocolo no ambiente fictício." }
});
const rota = "/api/ordens/" + criacao.dados.id;
const abrirDetalhe = async cookie => (await chamar(rota, { cookie })).dados;
const responsavel = (await chamar("/api/ordens/opcoes", { cookie: fornecedora })).dados[0];
const aberta = (await abrirDetalhe(alfa)).ordem;
assert.equal(aberta.situacao, "Aberta");
assert(Number.isInteger(aberta.solicitanteId) && aberta.solicitanteId > 0);
assert.equal((await abrirDetalhe(alfa)).ordem.previsao, null);
await chamar(rota, { cookie: beta, esperado: 404 });

const triagem = { situacao: "EmAnalise", prioridade: "Alta", responsavelId: responsavel.id, previsao: "2026-12-10" };
await chamar(rota + "/triagem", { cookie: alfa, metodo: "PUT", corpo: triagem, esperado: 403 });
await chamar(rota + "/triagem", { cookie: fornecedora, metodo: "PUT", corpo: triagem, esperado: 204 });
assert.equal((await abrirDetalhe(alfa)).ordem.previsao, triagem.previsao);
await chamar(rota + "/mensagens", { cookie: fornecedora, metodo: "POST", esperado: 204,
    corpo: { tipo: "SolicitacaoVpn", texto: "Solicito acesso temporário conforme o procedimento do cliente." } });
await chamar(rota + "/mensagens", { cookie: alfa, metodo: "POST", esperado: 204,
    corpo: { tipo: "Mensagem", texto: "Solicitação recebida e encaminhada para análise." } });
assert.equal((await abrirDetalhe(alfa)).mensagens.length, 2);
await chamar(rota + "/triagem", { cookie: fornecedora, metodo: "PUT", esperado: 204,
    corpo: { ...triagem, situacao: "EmExecucao" } });

async function enviar(versao, gmud) {
    await chamar(rota + "/entregas", { cookie: fornecedora, metodo: "POST", esperado: 204,
        corpo: { versao, gmud, linkGmud: null, resumoTestes: "Conferência do protocolo em teste." } });
    const detalhe = await abrirDetalhe(alfa);
    assert.equal(detalhe.ordem.situacao, "EmHomologacao");
    return detalhe.entregas.find(item => item.versao === versao).id;
}

const primeira = await enviar("1.0.0", "GMUD-01");
await chamar(rota + "/entregas/" + primeira + "/homologacao", { cookie: alfa, metodo: "POST", esperado: 400,
    corpo: { resultado: "Devolvida", motivo: "" } });
await chamar(rota + "/entregas/" + primeira + "/homologacao", { cookie: alfa, metodo: "POST", esperado: 204,
    corpo: { resultado: "Devolvida", motivo: "A data do protocolo permanece incorreta." } });
assert.equal((await abrirDetalhe(alfa)).ordem.situacao, "EmExecucao");
const segunda = await enviar("1.0.1", "GMUD-02");
await chamar(rota + "/entregas/" + segunda + "/homologacao", { cookie: beta, metodo: "POST", esperado: 404,
    corpo: { resultado: "Aprovada" } });
await chamar(rota + "/entregas/" + segunda + "/homologacao", { cookie: alfa, metodo: "POST", esperado: 204,
    corpo: { resultado: "Aprovada" } });
const detalhe = await abrirDetalhe(alfa);
assert.equal(detalhe.ordem.situacao, "Concluida");
assert.deepEqual(detalhe.entregas.map(item => item.resultado), ["Aprovada", "Devolvida"]);
assert(detalhe.historico.some(evento => evento.evento === "OS aberta"));
assert.equal(detalhe.ordem.versoesDecididas, 2);
assert.equal(detalhe.ordem.versoesDevolvidas, 1);
const cadastroId = Date.now();
const novoEmail = "ti.alfa." + cadastroId + "@portal.test";
const novaConta = { nome: "TI Alfa cadastrada", email: novoEmail, empresa: "Cliente Alfa",
    senha: "Senha de demonstração 2026" };
const fila = "/api/cadastro/solicitacoes";
const perfil = "/api/acesso/perfil";
const diretorio = "/api/acesso/usuarios";

await chamar("/api/cadastro", { metodo: "POST", esperado: 400,
    corpo: { ...novaConta, empresa: "Empresa Inexistente " + cadastroId } });
await chamar("/api/cadastro", { metodo: "POST", esperado: 204, corpo: novaConta });
await chamar("/api/cadastro", { metodo: "POST", esperado: 409, corpo: novaConta });

const credenciais = { email: novoEmail, senha: novaConta.senha };
const pendente = await chamar("/api/acesso/login", {
    metodo: "POST", esperado: 403, corpo: credenciais
});
assert.match(pendente.dados.mensagem, /aguardando aprovação/i);
assert.equal(pendente.cookie, undefined);
await chamar("/api/acesso/login", { metodo: "POST", esperado: 401,
    corpo: { ...credenciais, senha: "Senha diferente da cadastrada" } });

await chamar(fila, { esperado: 401 });
await chamar(fila, { cookie: alfa, esperado: 403 });
await chamar(diretorio, { esperado: 401 });
const pedidos = (await chamar(fila, { cookie: fornecedora })).dados;
const pedido = pedidos.find(item => item.email === novoEmail);
assert.equal(pedido.empresa, "Cliente Alfa");
assert.equal(pedido.senhaHash, undefined);

async function decidir(id, aprovar, cookie, esperado = 204) {
    return chamar(fila + "/" + id + "/decisao", { metodo: "POST", esperado, cookie, corpo: { aprovar } });
}

await decidir(pedido.id, true, beta, 403);
await decidir(pedido.id, true, fornecedora);
await decidir(pedido.id, true, fornecedora, 409);

const colabora = await login(novoEmail, novaConta.senha);
const contaAlfa = (await chamar("/api/acesso/", { cookie: colabora })).dados;
assert.equal(contaAlfa.empresa, "Cliente Alfa");
assert.equal(contaAlfa.perfil, "TI do cliente");
assert.equal((await abrirDetalhe(colabora)).ordem.id, criacao.dados.id);
assert((await chamar("/api/ordens", { cookie: colabora })).dados.some(ordem => ordem.id === criacao.dados.id));
assert(!(await chamar("/api/ordens", { cookie: beta })).dados.some(ordem => ordem.id === criacao.dados.id));

async function equipe(cookie) {
    return (await chamar(diretorio, { cookie })).dados;
}

const usuariosAlfa = await equipe(colabora);
const usuariosBeta = await equipe(beta);
assert.equal(usuariosAlfa.empresa, "Cliente Alfa");
assert.equal(usuariosBeta.empresa, "Cliente Beta");
assert(usuariosAlfa.usuarios.some(usuario => usuario.email === "ti.alfa@portal.test"));
assert(usuariosAlfa.usuarios.some(usuario => usuario.email === novoEmail));
assert(!usuariosAlfa.usuarios.some(usuario => usuario.email === "ti.beta@portal.test"));
assert(usuariosBeta.usuarios.some(usuario => usuario.email === "ti.beta@portal.test"));
assert(!usuariosBeta.usuarios.some(usuario => usuario.email === novoEmail));
assert(usuariosAlfa.usuarios.every(usuario => usuario.senhaHash === undefined));

const perfilAtual = (await chamar(perfil, { cookie: colabora })).dados;
assert.equal(perfilAtual.nome, novaConta.nome);
assert.equal(perfilAtual.email, novoEmail);
const novoNome = "TI Alfa atualizada";
const alteracao = (await chamar(perfil, { cookie: colabora, metodo: "PUT",
    corpo: { nome: novoNome, email: novoEmail } })).dados;
assert.equal(alteracao.nome, novoNome);
assert.equal(alteracao.empresa, "Cliente Alfa");

const emailAlterado = "ti.alfa.editado." + cadastroId + "@portal.test";
const senhaAlterada = "Senha alterada em demonstração 2026";
const dadosAlterados = { nome: novoNome, email: emailAlterado, senhaNova: senhaAlterada };
await chamar(perfil, { cookie: colabora, metodo: "PUT", esperado: 400, corpo: dadosAlterados });
await chamar(perfil, { cookie: colabora, metodo: "PUT", esperado: 409,
    corpo: { ...dadosAlterados, email: "ti.beta@portal.test", senhaAtual: novaConta.senha } });
await chamar(perfil, { cookie: colabora, metodo: "PUT",
    corpo: { ...dadosAlterados, senhaAtual: novaConta.senha } });
await chamar("/api/acesso/login", { metodo: "POST", esperado: 401, corpo: credenciais });
const novoCookie = await login(emailAlterado, senhaAlterada);
assert.equal((await chamar(perfil, { cookie: novoCookie })).dados.email, emailAlterado);
assert((await equipe(alfa)).usuarios.some(usuario => usuario.email === emailAlterado && usuario.nome === novoNome));

const emailRecusado = "ti.alfa.recusado." + cadastroId + "@portal.test";
await chamar("/api/cadastro", { metodo: "POST", esperado: 204, corpo: { ...novaConta, email: emailRecusado } });
const recusado = (await chamar(fila, { cookie: fornecedora })).dados.find(item => item.email === emailRecusado);
await decidir(recusado.id, false, fornecedora);
await chamar("/api/acesso/login", { metodo: "POST", esperado: 401,
    corpo: { ...credenciais, email: emailRecusado } });
assert(!(await equipe(alfa)).usuarios.some(usuario => usuario.email === emailRecusado));

const rotaEmpresas = "/api/empresas";
const nomeEmpresa = "Cliente de teste " + cadastroId;
await chamar(rotaEmpresas, { esperado: 401 });
await chamar(rotaEmpresas, { cookie: alfa, esperado: 403 });
await chamar(rotaEmpresas, { cookie: alfa, metodo: "POST", corpo: { nome: nomeEmpresa }, esperado: 403 });
await chamar(rotaEmpresas, { cookie: fornecedora, metodo: "POST", corpo: { nome: "   " }, esperado: 400 });

const registro = (await chamar(rotaEmpresas, {
    cookie: fornecedora, metodo: "POST", corpo: { nome: nomeEmpresa }, esperado: 201
})).dados;
assert.equal(registro.nome, nomeEmpresa);
assert.match(registro.codigo, /^cli-[a-f0-9]{16}$/);
assert.deepEqual(registro.sistemas, []);
await chamar(rotaEmpresas, {
    cookie: fornecedora, metodo: "POST", corpo: { nome: nomeEmpresa.toUpperCase() }, esperado: 409
});

const adicionarSistema = async (id, nome, cookie, esperado = 201) => (await chamar(
    rotaEmpresas + "/" + id + "/sistemas", { cookie, metodo: "POST", corpo: { nome }, esperado }
)).dados;
await adicionarSistema(registro.id, "TGS - E/I", beta, 403);
await adicionarSistema(2147483647, "TGS - E/I", fornecedora, 404);
const sistemaNovo = await adicionarSistema(registro.id, "TGS - E/I", fornecedora);
await adicionarSistema(registro.id, "tgs - e/i", fornecedora, 409);
const empresasDoPortal = (await chamar(rotaEmpresas, { cookie: fornecedora })).dados;
const empresaCriada = empresasDoPortal.find(empresa => empresa.id === registro.id);
assert.equal(empresaCriada.codigo, registro.codigo);
assert.deepEqual(empresaCriada.sistemas, [sistemaNovo]);

const contaEmpresa = { ...novaConta, email: "ti.empresa." + cadastroId + "@portal.test", empresa: registro.codigo };
await chamar("/api/cadastro", { metodo: "POST", esperado: 204, corpo: contaEmpresa });
const pedidoEmpresa = (await chamar(fila, { cookie: fornecedora })).dados.find(item => item.email === contaEmpresa.email);
assert.equal(pedidoEmpresa.empresa, nomeEmpresa);
await decidir(pedidoEmpresa.id, true, fornecedora);
const clienteNovo = await login(contaEmpresa.email, contaEmpresa.senha);
assert.equal((await chamar("/api/acesso/", { cookie: clienteNovo })).dados.empresaId, registro.id);
assert.deepEqual((await chamar("/api/ordens/opcoes", { cookie: clienteNovo })).dados, [sistemaNovo]);
const ordemNova = (await chamar("/api/ordens", {
    cookie: clienteNovo, metodo: "POST", esperado: 201,
    corpo: { sistemaId: sistemaNovo.id, tipo: "Correcao", titulo: "OS de " + nomeEmpresa,
        descricao: "Fluxo de abertura de OS por uma empresa recém-cadastrada." }
})).dados;
await chamar("/api/ordens/" + ordemNova.id, { cookie: clienteNovo });
await chamar("/api/ordens/" + ordemNova.id, { cookie: fornecedora });
await chamar("/api/ordens/" + ordemNova.id, { cookie: beta, esperado: 404 });
const ordensFornecedor = (await chamar("/api/ordens", { cookie: fornecedora })).dados;
const ordensClienteNovo = (await chamar("/api/ordens", { cookie: clienteNovo })).dados;
const novoSolicitante = ordensFornecedor.find(ordem => ordem.id === ordemNova.id).solicitanteId;
assert(Number.isInteger(novoSolicitante) && novoSolicitante > 0);
assert.notEqual(novoSolicitante, aberta.solicitanteId);
assert.equal(ordensFornecedor.find(ordem => ordem.id === criacao.dados.id).responsavelId, responsavel.id);
assert(!ordensClienteNovo.some(ordem => ordem.solicitanteId === aberta.solicitanteId));
assert.equal((await equipe(clienteNovo)).empresa, nomeEmpresa);

await conferirRedirecionamento("/empresas.html", "/login.html");
assert.equal((await abrirPagina("/empresas.html", beta)).status, 403);
const telaLiberada = await abrirPagina("/empresas.html", fornecedora);
assert.equal(telaLiberada.status, 200);
assert.match(await telaLiberada.text(), /id="form-empresa"/);

const empresaApi = rotaEmpresas + "/" + registro.id;
const nomeEditado = nomeEmpresa + " editado";
const salvar = async (rota, corpo, cookie = fornecedora, esperado = 204) =>
    chamar(rota, { cookie, metodo: "PUT", corpo, esperado });
await salvar(empresaApi, { nome: nomeEditado }, beta, 403);
await salvar(empresaApi, { nome: "Cliente Alfa" }, fornecedora, 409);
await salvar(empresaApi, { nome: nomeEditado });
const empresasAtualizadas = (await chamar(rotaEmpresas, { cookie: fornecedora })).dados;
const empresaEditada = empresasAtualizadas.find(empresa => empresa.id === registro.id);
assert.equal(empresaEditada.nome, nomeEditado);
assert.equal(empresaEditada.codigo, registro.codigo);
assert.equal(empresaEditada.ativa, true);
assert.equal((await chamar("/api/acesso/", { cookie: clienteNovo })).dados.empresa, nomeEditado);

const rotaContas = "/api/cadastro/contas";
const manual = { empresaId: registro.id, nome: "Conta manual " + cadastroId,
    email: "manual." + cadastroId + "@portal.test", senha: "Senha inicial de demonstração 2026" };
await chamar(rotaContas, { esperado: 401 });
await chamar(rotaContas, { cookie: beta, esperado: 403 });
await chamar(rotaContas, { cookie: beta, metodo: "POST", corpo: manual, esperado: 403 });
await chamar(rotaContas, { cookie: fornecedora, metodo: "POST", corpo: { ...manual, empresaId: 2147483647 }, esperado: 404 });
const idManual = (await chamar(rotaContas, {
    cookie: fornecedora, metodo: "POST", corpo: manual, esperado: 201
})).dados.id;
await chamar(rotaContas, { cookie: fornecedora, metodo: "POST", corpo: manual, esperado: 409 });
const contasCadastradas = async () => (await chamar(rotaContas, { cookie: fornecedora })).dados;
const manualCadastrada = (await contasCadastradas()).find(conta => conta.id === idManual);
assert.equal(manualCadastrada.ativo, true);
assert.equal(manualCadastrada.empresaId, registro.id);
assert.equal(manualCadastrada.senhaHash, undefined);
const sessaoManual = await login(manual.email, manual.senha);
assert.equal((await equipe(sessaoManual)).empresa, nomeEditado);

const contaApi = rotaContas + "/" + idManual;
const dadosEditados = { nome: "Conta manual editada", email: "edit." + manual.email,
    senhaNova: "Senha alterada por fornecedora 2026" };
await salvar(contaApi, dadosEditados, beta, 403);
await salvar(contaApi, { ...dadosEditados, email: "ti.beta@portal.test" }, fornecedora, 409);
await salvar(contaApi, dadosEditados);
await chamar("/api/acesso/perfil", { cookie: sessaoManual, esperado: 401 });
const sessaoManualEditada = await login(dadosEditados.email, dadosEditados.senhaNova);
assert.equal((await chamar("/api/acesso/perfil", { cookie: sessaoManualEditada })).dados.nome, dadosEditados.nome);

async function loginBloqueado(email, senha, mensagem) {
    const bloqueado = await chamar("/api/acesso/login", { metodo: "POST", corpo: { email, senha }, esperado: 403 });
    assert.match(bloqueado.dados.mensagem, mensagem);
    assert.equal(bloqueado.cookie, undefined);
}

await salvar(contaApi + "/situacao", { ativo: false }, beta, 403);
await salvar(contaApi + "/situacao", { ativo: false });
await chamar("/api/ordens", { cookie: sessaoManualEditada, esperado: 401 });
await loginBloqueado(dadosEditados.email, dadosEditados.senhaNova, /conta está desativada/);
assert(!(await equipe(clienteNovo)).usuarios.some(conta => conta.email === dadosEditados.email));

const novoPedido = { ...contaEmpresa, email: "pendente." + cadastroId + "@portal.test" };
await chamar("/api/cadastro", { metodo: "POST", corpo: novoPedido, esperado: 204 });
const pedidoPendente = (await chamar(fila, { cookie: fornecedora })).dados.find(conta => conta.email === novoPedido.email);
await salvar(empresaApi + "/situacao", { ativa: false });
await chamar("/api/ordens", { cookie: clienteNovo, esperado: 401 });
await loginBloqueado(contaEmpresa.email, contaEmpresa.senha, /empresa está desativada/);
await chamar("/api/ordens/" + ordemNova.id, { cookie: fornecedora });
assert.equal((await contasCadastradas()).find(conta => conta.id === idManual).empresaAtiva, false);
await decidir(pedidoPendente.id, true, fornecedora, 409);
await chamar("/api/cadastro", { metodo: "POST",
    corpo: { ...novoPedido, email: "bloqueado." + novoPedido.email }, esperado: 400 });
await chamar(rotaContas, { cookie: fornecedora, metodo: "POST",
    corpo: { ...manual, email: "bloqueado." + manual.email }, esperado: 409 });

await salvar(empresaApi + "/situacao", { ativa: true });
await chamar("/api/ordens", { cookie: clienteNovo, esperado: 401 });
const sessaoReativada = await login(contaEmpresa.email, contaEmpresa.senha);
await chamar("/api/ordens/" + ordemNova.id, { cookie: sessaoReativada });
await decidir(pedidoPendente.id, true, fornecedora);
await login(novoPedido.email, novoPedido.senha);
await loginBloqueado(dadosEditados.email, dadosEditados.senhaNova, /conta está desativada/);
await salvar(contaApi + "/situacao", { ativo: true });
await chamar("/api/ordens", { cookie: sessaoManualEditada, esperado: 401 });
await login(dadosEditados.email, dadosEditados.senhaNova);

const apiEquipe = "/api/equipe/usuarios";
const apiIdentidade = "/api/equipe/identidade";
const identidadeAntes = (await chamar("/api/acesso/perfil", { cookie: fornecedora })).dados;
assert.equal((await chamar("/api/acesso/", { cookie: fornecedora })).dados.administrador, true);
await conferirRedirecionamento("/equipe.html", "/login.html");
assert.equal((await abrirPagina("/equipe.html", alfa)).status, 403);
const paginaEquipe = await abrirPagina("/equipe.html", fornecedora);
assert.equal(paginaEquipe.status, 200);
assert.match(await paginaEquipe.text(), /id="form-usuario-equipe"/);
await chamar(apiEquipe, { esperado: 401 });
await chamar(apiEquipe, { cookie: alfa, esperado: 403 });

const novaEquipe = { nome: "Consultor ADM", email: "equipe." + cadastroId + "@portal.test",
    senha: "Senha de equipe fornecedora 2026" };
const idEquipe = (await chamar(apiEquipe, { cookie: fornecedora, metodo: "POST", corpo: novaEquipe, esperado: 201 })).dados.id;
await chamar(apiEquipe, { cookie: fornecedora, metodo: "POST", corpo: novaEquipe, esperado: 409 });
const membros = (await chamar(apiEquipe, { cookie: fornecedora })).dados;
const admin = membros.find(membro => membro.administrador);
assert(admin);
const membroEquipe = membros.find(membro => membro.id === idEquipe);
assert.equal(membroEquipe.nome, novaEquipe.nome);
assert.equal(membroEquipe.administrador, false);
assert.equal(membroEquipe.ativo, true);
assert.equal(membroEquipe.senhaHash, undefined);
await salvar(apiEquipe + "/" + admin.id + "/situacao", { ativo: false }, fornecedora, 409);

const sessaoEquipe = await login(novaEquipe.email, novaEquipe.senha);
const sessaoInterna = (await chamar("/api/acesso/", { cookie: sessaoEquipe })).dados;
assert.equal(sessaoInterna.fornecedora, true);
assert.equal(sessaoInterna.administrador, false);
assert.equal((await abrirPagina("/equipe.html", sessaoEquipe)).status, 403);
assert.doesNotMatch(await (await abrirPagina("/perfil.html", sessaoEquipe)).text(), /id="form-identidade"/);
assert.match(await (await abrirPagina("/perfil.html", fornecedora)).text(), /id="form-identidade"/);
await chamar(apiEquipe, { cookie: sessaoEquipe, metodo: "POST", corpo: novaEquipe, esperado: 403 });
await salvar(apiEquipe + "/" + idEquipe, { nome: "Teste", email: novaEquipe.email }, sessaoEquipe, 403);
await salvar(apiEquipe + "/" + idEquipe + "/situacao", { ativo: false }, sessaoEquipe, 403);

const novaIdentidade = { empresa: "Fornecedora de validação", perfil: "Equipe de sustentação" };
await salvar(apiIdentidade, novaIdentidade, sessaoEquipe, 403);
await salvar(apiIdentidade, novaIdentidade, alfa, 403);
try {
    await salvar(apiIdentidade, novaIdentidade, fornecedora, 200);
    const dados = (await chamar("/api/acesso/", { cookie: sessaoEquipe })).dados;
    assert.equal(dados.empresa, novaIdentidade.empresa);
    assert.equal(dados.perfil, novaIdentidade.perfil);
    assert.equal(dados.fornecedora, true);
    assert.equal((await chamar("/api/acesso/perfil", { cookie: alfa })).dados.perfil, "TI do cliente");
} finally {
    await salvar(apiIdentidade, { empresa: identidadeAntes.empresa, perfil: identidadeAntes.perfil }, fornecedora, 200);
}

const dadosEquipe = { nome: "Analista de sustentação", email: "edit." + novaEquipe.email,
    senhaNova: "Nova senha de equipe fornecedora 2026" };
await salvar(apiEquipe + "/" + idEquipe, dadosEquipe);
await chamar("/api/acesso/", { cookie: sessaoEquipe, esperado: 401 });
const sessaoEquipeEditada = await login(dadosEquipe.email, dadosEquipe.senhaNova);
assert.equal((await chamar("/api/acesso/perfil", { cookie: sessaoEquipeEditada })).dados.nome, dadosEquipe.nome);
assert((await chamar("/api/ordens/opcoes", { cookie: fornecedora })).dados.some(usuario => usuario.id === idEquipe));
await salvar(apiEquipe + "/" + idEquipe + "/situacao", { ativo: false });
await chamar("/api/ordens", { cookie: sessaoEquipeEditada, esperado: 401 });
await loginBloqueado(dadosEquipe.email, dadosEquipe.senhaNova, /conta está desativada/);
assert(!(await chamar("/api/ordens/opcoes", { cookie: fornecedora })).dados.some(usuario => usuario.id === idEquipe));
await salvar(apiEquipe + "/" + idEquipe + "/situacao", { ativo: true });
await chamar("/api/ordens", { cookie: sessaoEquipeEditada, esperado: 401 });
await login(dadosEquipe.email, dadosEquipe.senhaNova);

const rotaIndicadores = "/api/indicadores";
const novoIndicador = { nome: "Em execução " + cadastroId, criterio: "EmExecucao" };
await conferirRedirecionamento("/indicadores.html", "/login.html");
assert.equal((await abrirPagina("/indicadores.html", alfa)).status, 403);
const paginaIndicadores = await abrirPagina("/indicadores.html", fornecedora);
assert.equal(paginaIndicadores.status, 200);
assert.match(await paginaIndicadores.text(), /id="form-indicador"/);

await chamar(rotaIndicadores, { esperado: 401 });
await chamar(rotaIndicadores, { cookie: alfa, metodo: "POST", corpo: novoIndicador, esperado: 403 });
await chamar(rotaIndicadores, { cookie: fornecedora, metodo: "POST",
    corpo: { ...novoIndicador, criterio: "Invalido" }, esperado: 400 });
const indicador = (await chamar(rotaIndicadores, { cookie: fornecedora, metodo: "POST",
    corpo: novoIndicador, esperado: 201 })).dados;
assert(Number.isInteger(indicador.id));
assert.equal(indicador.nome, novoIndicador.nome);
assert.equal(indicador.criterio, novoIndicador.criterio);
await chamar(rotaIndicadores, { cookie: fornecedora, metodo: "POST",
    corpo: { ...novoIndicador, nome: novoIndicador.nome.toUpperCase() }, esperado: 409 });
for (const cookie of [fornecedora, alfa]) {
    const cartoes = (await chamar(rotaIndicadores, { cookie })).dados;
    assert.deepEqual(cartoes.find(cartao => cartao.id === indicador.id), indicador);
}

const valoresIndicadores = (await chamar(rotaIndicadores, { cookie: fornecedora })).dados;
assert.deepEqual(valoresIndicadores.filter(cartao => cartao.padrao).map(cartao => cartao.id), [-3, -2, -1]);
assert.equal(indicador.padrao, false);
const rotaEdicao = rotaIndicadores + "/" + indicador.id;
const alterado = { nome: "Ordens concluídas " + cadastroId, criterio: "Concluida" };
await salvar(rotaEdicao, alterado, alfa, 403);
await salvar(rotaEdicao, { ...alterado, criterio: "Invalido" }, fornecedora, 400);
await salvar(rotaIndicadores + "/-99", alterado, fornecedora, 404);
assert.deepEqual((await salvar(rotaEdicao, alterado, fornecedora, 200)).dados, {
    id: indicador.id, ...alterado, padrao: false
});
const original = valoresIndicadores.find(cartao => cartao.id === -3);
const padraoEditado = { nome: "Casos sem responsável " + cadastroId, criterio: "SemResponsavel" };
try {
    assert.deepEqual((await salvar(rotaIndicadores + "/-3", padraoEditado, fornecedora, 200)).dados, {
        id: -3, ...padraoEditado, padrao: true
    });
    const vistosPeloCliente = (await chamar(rotaIndicadores, { cookie: alfa })).dados;
    assert.deepEqual(vistosPeloCliente.find(cartao => cartao.id === -3), { id: -3, ...padraoEditado, padrao: true });
    assert.equal(vistosPeloCliente.filter(cartao => cartao.id === -3).length, 1);
    assert.deepEqual(vistosPeloCliente.find(cartao => cartao.id === indicador.id), {
        id: indicador.id, ...alterado, padrao: false
    });
} finally {
    await salvar(rotaIndicadores + "/-3", { nome: original.nome, criterio: original.criterio }, fornecedora, 200);
}

async function conferirSubmenus(rota, cookie, destinos) {
    const pagina = await abrirPagina(rota, cookie);
    assert.equal(pagina.status, 200, rota);
    const html = await pagina.text();
    assert.match(html, /class="menu-lateral"/, rota);
    assert(html.includes('href="' + rota + '" aria-current="page"'), rota);
    for (const destino of destinos) {
        assert(html.includes('href="#' + destino + '"'), rota + " submenu " + destino);
        assert(html.includes('id="' + destino + '"'), rota + " seção " + destino);
    }
    return html;
}

await conferirSubmenus("/ordens.html", alfa, ["nova", "metricas", "filtros-ordens", "lista-ordens"]);
await conferirSubmenus("/empresas.html", fornecedora, ["nova-empresa", "empresas-cadastradas"]);
await conferirSubmenus("/aprovacoes.html", fornecedora, ["solicitacoes-pendentes", "criar-conta-manual", "contas-cadastradas"]);
await conferirSubmenus("/indicadores.html", fornecedora, ["novo-indicador", "indicadores-cadastrados"]);
await conferirSubmenus("/equipe.html", fornecedora, ["novo-usuario", "usuarios-fornecedora", "consultor-adm"]);
await conferirSubmenus("/usuarios.html", alfa, ["diretorio-usuarios"]);
const perfilCliente = await conferirSubmenus("/perfil.html", alfa, ["dados-perfil", "seguranca-perfil"]);
assert.doesNotMatch(perfilCliente, /href="\/(?:empresas|aprovacoes|indicadores|equipe)\.html"/);
await conferirSubmenus("/perfil.html", fornecedora, ["dados-perfil", "seguranca-perfil", "identidade-fornecedora"]);
await conferirSubmenus("/guia.html", alfa, ["guia-acesso", "guia-abrir", "guia-triagem", "guia-mensagens", "guia-convivencia", "guia-termos"]);
await conferirSubmenus("/ajuda.html", alfa, ["orientacao-ajuda", "novo-relato", "acompanhar-relatos"]);
const guiaPublico = await abrirPagina("/guia.html");
assert.equal(guiaPublico.status, 200);
assert.doesNotMatch(await guiaPublico.text(), /class="menu-lateral"/);

const ajudaPublica = await abrirPagina("/ajuda.html");
assert.equal(ajudaPublica.status, 200);
const ajudaHtml = await ajudaPublica.text();
assert.match(ajudaHtml, /href="#conteudo-principal"/);
assert.match(ajudaHtml, /<main id="conteudo-principal" tabindex="-1"/);
assert.doesNotMatch(ajudaHtml, /id="form-relato"/);
await chamar("/api/relatos", { esperado: 401 });
await chamar("/api/relatos", { cookie: alfa, metodo: "POST",
    corpo: { categoria: "Acessibilidade", descricao: "" }, esperado: 400 });
await chamar("/api/relatos", { cookie: beta, metodo: "POST",
    corpo: { categoria: "Acessibilidade", descricao: "Teste de escopo.", ordemId: criacao.dados.id }, esperado: 404 });

const descricaoRelato = "Teste de navegação por teclado " + cadastroId;
const registroRelato = (await chamar("/api/relatos", { cookie: alfa, metodo: "POST", esperado: 201,
    corpo: { categoria: "Acessibilidade", descricao: descricaoRelato, ordemId: criacao.dados.id } })).dados;
const consultarRelato = async cookie =>
    (await chamar("/api/relatos", { cookie })).dados.find(relato => relato.id === registroRelato.id);
const inicial = await consultarRelato(alfa);
assert.equal(inicial.descricao, descricaoRelato);
assert.equal(inicial.ordemId, criacao.dados.id);
assert.equal(inicial.situacao, "Recebido");
assert.equal(inicial.podeTratar, false);
assert.equal(inicial.meu, true);

assert.equal(await consultarRelato(novoCookie), undefined, "Colega do cliente não vê o relato");
assert.equal(await consultarRelato(beta), undefined, "Outro cliente não vê o relato");
const equipeSemPermissao = await login(dadosEquipe.email, dadosEquipe.senhaNova);
assert.equal(await consultarRelato(equipeSemPermissao), undefined, "Equipe sem administração não vê o relato");
const recebido = await consultarRelato(fornecedora);
assert.equal(recebido.podeTratar, true);
assert.equal(recebido.meu, false);

const estadoRelato = "/api/relatos/" + registroRelato.id + "/situacao";
await salvar(estadoRelato, { situacao: "EmAnalise" }, alfa, 403);
await salvar(estadoRelato, { situacao: "EmAnalise" }, equipeSemPermissao, 403);
await salvar(estadoRelato, { situacao: "EmAnalise" });
assert.equal((await consultarRelato(alfa)).situacao, "EmAnalise");
await salvar(estadoRelato, { situacao: "Respondido", resposta: "" }, fornecedora, 400);
await salvar(estadoRelato, { situacao: "Respondido", resposta: "O problema foi encaminhado para correção." });
const respondido = await consultarRelato(alfa);
assert.equal(respondido.situacao, "Respondido");
assert.match(respondido.resposta, /encaminhado/);
await salvar(estadoRelato, { situacao: "EmAnalise" }, fornecedora, 409);

console.log("Fluxo integrado com ordens, navegação, permissões, indicadores e relatos reservados.");
