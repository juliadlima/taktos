"use strict";

const $ = seletor => document.querySelector(seletor);
const estados = {
    Aberta: "Aberta", EmAnalise: "Em análise", EmExecucao: "Em execução",
    EmHomologacao: "Em homologação", Concluida: "Concluída"
};
const proximas = { Aberta: "EmAnalise", EmAnalise: "EmExecucao" };
const prioridades = { Baixa: "Baixa", Media: "Média", Alta: "Alta" };
const tipos = { Correcao: "Correção", Melhoria: "Melhoria" };
const formatoContagem = new Intl.NumberFormat("pt-BR");
const formatoDia = new Intl.DateTimeFormat("pt-BR");
const formatoHora = new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short", timeStyle: "short", timeZone: "America/Sao_Paulo"
});
const diaLocal = new Intl.DateTimeFormat("sv-SE", {
    year: "numeric", month: "2-digit", day: "2-digit", timeZone: "America/Sao_Paulo"
});
let sessao, detalheAtual;
let ordens = [], opcoes = [], indicadores = [];
let kanban = !globalThis.matchMedia?.("(max-width: 49rem)")?.matches;

const filtrosPessoas = [
    {
        campo: "solicitante", selecao: new Set(), id: ordem => ordem.solicitanteId,
        nome: ordem => ordem.solicitante + " · " + ordem.empresa
    },
    {
        campo: "responsavel", selecao: new Set(), id: ordem => ordem.responsavelId,
        nome: ordem => ordem.responsavel
    }
];

function elemento(tag, texto, classe) {
    const item = document.createElement(tag);
    if (texto !== undefined) item.textContent = texto;
    if (classe) item.className = classe;
    return item;
}

function data(valor) {
    return valor ? formatoDia.format(new Date(valor + "T12:00:00")) : "Aguardando previsão";
}

function hora(valor) {
    return formatoHora.format(new Date(valor));
}

function numero(id) {
    return "OS " + String(id).padStart(4, "0");
}

function erro(texto = "") {
    $("#mensagem").textContent = texto;
}

async function api(rota, metodo = "GET", dados) {
    const opcoes = { method: metodo };
    if (dados !== undefined) {
        opcoes.headers = { "Content-Type": "application/json" };
        opcoes.body = JSON.stringify(dados);
    }
    const resposta = await fetch(rota, opcoes);
    if (resposta.status === 401) {
        location.replace("/login.html");
        throw new Error("Entre novamente para acessar o Taktos.");
    }
    if (!resposta.ok) {
        const detalhe = await resposta.json().catch(() => null);
        const falha = new Error(detalhe?.mensagem || detalhe?.detail ||
            (resposta.status === 403 ? "Seu perfil não tem permissão para essa ação." : "Não foi possível concluir a operação."));
        falha.campo = detalhe?.campo;
        throw falha;
    }
    return resposta.status === 204 ? null : resposta.json();
}

function opcoesSeletor(seletor, itens, titulo) {
    const atual = seletor.value;
    seletor.replaceChildren();
    if (titulo) seletor.add(new Option(titulo, ""));
    for (const [valor, nome] of itens) seletor.add(new Option(nome, valor));
    if ([...seletor.options].some(opcao => opcao.value === atual)) seletor.value = atual;
}

function criarCartao(ordem, mostrandoKanban) {
    const cartao = elemento("article", undefined, "cartao");
    const situacao = elemento("span", estados[ordem.situacao], "tag");
    situacao.dataset.situacao = ordem.situacao;
    cartao.append(elemento("p", numero(ordem.id), "codigo-os"), elemento("h3", ordem.titulo));
    const tags = elemento("p");
    tags.append(situacao);
    if (ordem.prioridade) tags.append(elemento("span", "Prioridade " + prioridades[ordem.prioridade].toLowerCase(), "tag"));
    cartao.append(tags, elemento("p", ordem.empresa + " · " + ordem.sistema, "apoio"),
        elemento("p", ordem.previsao ? "Previsão " + data(ordem.previsao) : data(ordem.previsao), "previsao-cartao"));
    const acoes = elemento("div", undefined, "acoes");
    const abrir = elemento("button", "Ver detalhes", "secundario");
    abrir.type = "button";
    abrir.id = "os-" + (mostrandoKanban ? "kanban-" : "lista-") + ordem.id;
    abrir.setAttribute("aria-label", "Ver detalhes da " + numero(ordem.id));
    abrir.addEventListener("click", () => abrirDetalhe(ordem.id));
    acoes.append(abrir);

    if (mostrandoKanban && proximas[ordem.situacao]) {
        const proxima = elemento("button", estados[proximas[ordem.situacao]]);
        proxima.type = "button";
        proxima.setAttribute("aria-label", "Mover " + numero(ordem.id) + " para " + estados[proximas[ordem.situacao]]);
        proxima.addEventListener("click", async () => {
            if (ordem.situacao === "EmAnalise" && (!ordem.responsavelId || !ordem.prioridade)) {
                if (await abrirDetalhe(ordem.id)) {
                    $("#t-situacao").value = "EmExecucao";
                    $("#t-prioridade").focus();
                    $("#retorno").textContent = "Defina prioridade e responsável para iniciar a execução.";
                }
                return;
            }
            await executar(proxima, async () => {
                await api("/api/ordens/" + ordem.id + "/triagem", "PUT", {
                    situacao: proximas[ordem.situacao], prioridade: ordem.prioridade,
                    responsavelId: ordem.responsavelId, previsao: ordem.previsao
                });
                await atualizarLista();
                $("#retorno").textContent = numero(ordem.id) + " movida para " + estados[proximas[ordem.situacao]] + ".";
                focarOrdemNaVisao(ordem.id);
            });
        });
        acoes.append(proxima);
    }
    cartao.append(acoes);
    return cartao;
}

const cartoesPadrao = [
    { id: -3, campo: "m-previsao", criterio: "SemPrevisao" },
    { id: -2, campo: "m-prazo", criterio: "PrimeirasNoPrazo" },
    { id: -1, campo: "m-devolucao", criterio: "VersoesDevolvidas" }
];

function contarIndicador(criterio, resultados) {
    switch (criterio) {
        case "Total":
            return resultados.length;
        case "SemResponsavel":
            return resultados.filter(os => os.responsavelId == null).length;
        case "SemPrevisao":
            return resultados.filter(os => os.situacao !== "Concluida" && !os.previsao).length;
        case "PrimeirasNoPrazo":
            return resultados.filter(os => os.primeiraEntrega && os.primeiraPrevisao &&
                diaLocal.format(new Date(os.primeiraEntrega)) <= os.primeiraPrevisao).length;
        case "VersoesDevolvidas":
            return resultados.reduce((total, os) => total + os.versoesDevolvidas, 0);
        case "Correcao":
        case "Melhoria":
            return resultados.filter(os => os.tipo === criterio).length;
        default:
            return resultados.filter(os => os.situacao === criterio).length;
    }
}

function atualizarIndicadores(resultados) {
    for (const padrao of cartoesPadrao) {
        const indicador = indicadores.find(item => item.id === padrao.id);
        const criterio = indicador?.criterio ?? padrao.criterio;
        if (indicador) $("#nome-" + padrao.campo).textContent = indicador.nome;
        $("#" + padrao.campo).textContent = formatoContagem.format(contarIndicador(criterio, resultados));
    }

    const personalizados = indicadores.filter(indicador => !indicador.padrao);
    $("#indicadores-personalizados").replaceChildren(...personalizados.map(indicador => {
        const cartao = elemento("div", undefined, "painel indicador");
        const quantidade = contarIndicador(indicador.criterio, resultados);
        cartao.append(elemento("span", indicador.nome), elemento("strong", formatoContagem.format(quantidade), "metrica"));
        return cartao;
    }));
}

function mostrarSelecaoPessoas(filtro) {
    const total = filtro.selecao.size;
    $("#resumo-" + filtro.campo).textContent = total === 0 ? "Todos" :
        total === 1 ? "1 selecionado" : total + " selecionados";
    $("#limpar-" + filtro.campo).hidden = total === 0;
}

function atualizarOpcoesPessoas(filtro) {
    const pessoas = new Map();
    for (const ordem of ordens) {
        const id = filtro.id(ordem);
        if (id != null) pessoas.set(id, filtro.nome(ordem));
    }

    for (const id of filtro.selecao) {
        if (!pessoas.has(id)) filtro.selecao.delete(id);
    }

    const opcoes = $("#opcoes-" + filtro.campo);
    opcoes.replaceChildren();
    if (pessoas.size === 0) {
        opcoes.append(elemento("p", "Nenhuma pessoa disponível.", "apoio"));
    }

    const contagemNomes = new Map();
    for (const nome of pessoas.values()) {
        contagemNomes.set(nome, (contagemNomes.get(nome) ?? 0) + 1);
    }
    const ordenadas = [...pessoas].sort(([idA, nomeA], [idB, nomeB]) => nomeA.localeCompare(nomeB, "pt-BR") || idA - idB);
    for (const [id, nome] of ordenadas) {
        const rotulo = contagemNomes.get(nome) > 1 ? nome + " · Usuário " + id : nome;
        const escolha = elemento("label", undefined, "opcao-pessoa");
        const caixa = elemento("input");
        caixa.type = "checkbox";
        caixa.checked = filtro.selecao.has(id);
        caixa.addEventListener("change", () => {
            if (caixa.checked)
                filtro.selecao.add(id);
            else
                filtro.selecao.delete(id);
            mostrarSelecaoPessoas(filtro);
            renderizarLista();
        });
        escolha.append(caixa, elemento("span", rotulo));
        opcoes.append(escolha);
    }

    mostrarSelecaoPessoas(filtro);
}

function renderizarLista() {
    const sistema = $("#f-sistema").value;
    const situacao = $("#f-situacao").value;
    const previsao = $("#f-previsao").value;
    const resultados = ordens.filter(os =>
        (!sistema || os.sistema === sistema) && (!situacao || os.situacao === situacao) &&
        (!previsao || Boolean(os.previsao) === (previsao === "definidas")) &&
        filtrosPessoas.every(filtro => filtro.selecao.size === 0 || filtro.selecao.has(filtro.id(os))));
    const mostrarQuadro = sessao.fornecedora && kanban;
    $("#quantidade").textContent = resultados.length + (resultados.length === 1 ? " ordem encontrada" : " ordens encontradas");
    $("#lista").hidden = mostrarQuadro;
    $("#kanban").hidden = !mostrarQuadro;
    $("#ver-lista").setAttribute("aria-pressed", String(!mostrarQuadro));
    $("#ver-kanban").setAttribute("aria-pressed", String(mostrarQuadro));
    atualizarIndicadores(resultados);

    if (!mostrarQuadro) {
        $("#lista").replaceChildren(...resultados.map(os => criarCartao(os, false)));
        if (resultados.length === 0) $("#lista").append(elemento("p", "Nenhuma OS encontrada."));
        return;
    }

    $("#kanban").replaceChildren();
    for (const [chave, nome] of Object.entries(estados)) {
        const coluna = elemento("section", undefined, "coluna");
        coluna.dataset.situacao = chave;
        const lista = resultados.filter(os => os.situacao === chave);
        const titulo = elemento("h2", nome);
        titulo.id = "coluna-" + chave;
        coluna.setAttribute("aria-labelledby", titulo.id);
        coluna.append(titulo, elemento("p", String(lista.length), "apoio"));
        for (const ordem of lista) coluna.append(criarCartao(ordem, true));
        $("#kanban").append(coluna);
    }
}

async function carregarIndicadores() {
    const aviso = $("#aviso-indicadores");
    try {
        const configurados = await api("/api/indicadores");
        aviso.hidden = true;
        return configurados;
    } catch {
        aviso.hidden = false;
        return indicadores;
    }
}

async function atualizarLista() {
    [ordens, indicadores] = await Promise.all([api("/api/ordens"), carregarIndicadores()]);
    const sistemas = [...new Set(ordens.map(os => os.sistema))].sort((a, b) => a.localeCompare(b, "pt-BR"));
    opcoesSeletor($("#f-sistema"), sistemas.map(nome => [nome, nome]), "Todos");
    for (const filtro of filtrosPessoas) atualizarOpcoesPessoas(filtro);
    renderizarLista();
}

function preencherDados(ordem) {
    const dados = Object.entries({
        Empresa: ordem.empresa, Sistema: ordem.sistema, Tipo: tipos[ordem.tipo],
        Solicitante: ordem.solicitante, Situação: estados[ordem.situacao],
        Prioridade: prioridades[ordem.prioridade] || "A definir",
        Responsável: ordem.responsavel || "A definir", Previsão: data(ordem.previsao),
        "Última atualização": hora(ordem.atualizadaEm)
    }).map(([chave, valor]) => {
        const item = elemento("div");
        item.append(elemento("dt", chave), elemento("dd", valor));
        return item;
    });
    $("#dados-os").replaceChildren(...dados);
    $("#descricao-os").textContent = ordem.descricao;
}

function preencherEntregas(entregas) {
    $("#entregas").replaceChildren();
    if (entregas.length === 0) $("#entregas").append(elemento("p", "Nenhuma versão registrada.", "apoio"));
    for (const entrega of entregas) {
        const item = elemento("article", undefined, "linha-item");
        item.append(elemento("h3", "Versão " + entrega.versao + " · " +
            (entrega.resultado === "Pendente" ? "Em homologação" : entrega.resultado)),
            elemento("p", entrega.autor + " · " + hora(entrega.criadaEm), "apoio"));
        const gmud = elemento("p", "GMUD: " + entrega.gmud);
        if (entrega.linkGmud?.startsWith("https://")) {
            const link = elemento("a", "Abrir documentação da GMUD");
            link.href = entrega.linkGmud;
            link.target = "_blank";
            link.rel = "noopener noreferrer";
            gmud.append(document.createTextNode(" · "), link);
        }
        item.append(gmud);
        if (entrega.resumoTestes) item.append(elemento("p", "Testes: " + entrega.resumoTestes, "quebra"));
        if (entrega.motivo) item.append(elemento("p", "Motivo: " + entrega.motivo, "quebra"));
        if (entrega.validadaEm) item.append(elemento("p", entrega.validadaPor + " · " + hora(entrega.validadaEm), "apoio"));
        $("#entregas").append(item);
    }
}

function preencherDetail(detalhe) {
    detalheAtual = detalhe;
    const { ordem, mensagens, entregas, historico } = detalhe;
    $("#detalhe-numero").textContent = numero(ordem.id);
    $("#detalhe-titulo").textContent = ordem.titulo;
    preencherDados(ordem);
    preencherEntregas(entregas);

    $("#mensagens").replaceChildren(...mensagens.map(mensagem => {
        const item = elemento("article", undefined, "linha-item");
        if (mensagem.tipo === "SolicitacaoVpn") item.append(elemento("p", "Solicitação de VPN", "tag"));
        item.append(elemento("p", mensagem.autor + " · " + hora(mensagem.criadaEm), "apoio"),
            elemento("p", mensagem.texto, "quebra"));
        return item;
    }));
    if (!mensagens.length) $("#mensagens").append(elemento("p", "Nenhuma mensagem registrada.", "apoio"));

    $("#historico").replaceChildren(...historico.map(evento => {
        const item = elemento("article", undefined, "linha-item");
        item.append(elemento("p", evento.evento),
            elemento("small", hora(evento.criadoEm) + (evento.autor ? " · " + evento.autor : "")));
        return item;
    }));

    const podeTriar = sessao.fornecedora && ["Aberta", "EmAnalise", "EmExecucao"].includes(ordem.situacao);
    $("#triagem").hidden = !podeTriar;
    if (podeTriar) {
        const escolhas = [ordem.situacao, proximas[ordem.situacao]].filter(Boolean);
        opcoesSeletor($("#t-situacao"), escolhas.map(valor => [valor, estados[valor]]));
        $("#t-situacao").value = ordem.situacao;
        $("#t-prioridade").value = ordem.prioridade || "";
        const responsaveis = opcoes.map(opcao => [String(opcao.id), opcao.nome]);
        if (ordem.responsavelId && !opcoes.some(opcao => opcao.id === ordem.responsavelId))
            responsaveis.push([String(ordem.responsavelId), ordem.responsavel + " (inativo)"]);
        opcoesSeletor($("#t-responsavel"), responsaveis, "A definir");
        $("#t-responsavel").value = ordem.responsavelId || "";
        $("#t-previsao").value = ordem.previsao || "";
    }
    $("#form-entrega").hidden = !sessao.fornecedora || ordem.situacao !== "EmExecucao";
    const pendente = entregas.find(entrega => entrega.resultado === "Pendente");
    $("#form-homologacao").hidden = sessao.fornecedora || !pendente;
    if (pendente) {
        $("#titulo-homologacao").textContent = "Validar a versão " + pendente.versao;
        $("#form-homologacao").elements.entregaId.value = pendente.id;
    }
}

async function abrirDetalhe(id, redirecionarFoco = true) {
    if (redirecionarFoco) erro();
    try {
        const detalhe = await api("/api/ordens/" + id);
        preencherDetail(detalhe);
        $("#visao").hidden = true;
        $("#detalhe").hidden = false;
        if (redirecionarFoco) $("#detalhe-titulo").focus();
        return true;
    } catch (falha) {
        erro(falha.message);
        return false;
    }
}

async function executar(botao, operacao, formulario) {
    erro();
    $("#retorno").textContent = "";
    botao.disabled = true;
    try {
        await operacao();
    } catch (falha) {
        if (formulario && falha.campo) globalThis.taktosCampos?.marcarErro(formulario, falha.campo, falha.message);
        erro(falha.message);
    } finally {
        botao.disabled = false;
    }
}

function lidarFormulario(seletor, operacao) {
    $(seletor).addEventListener("submit", evento => {
        evento.preventDefault();
        const formulario = evento.currentTarget;
        const botao = evento.submitter || formulario.querySelector('button[type="submit"]');
        const campos = Object.fromEntries(new FormData(formulario));
        executar(botao, () => operacao(formulario, campos), formulario);
    });
}

lidarFormulario("#form-nova", async (formulario, campos) => {
    campos.sistemaId = Number(campos.sistemaId);
    const ordem = await api("/api/ordens", "POST", campos);
    formulario.reset();
    $("#nova").hidden = true;
    await abrirDetalhe(ordem.id);
    $("#retorno").textContent = numero(ordem.id) + " registrada.";
});

lidarFormulario("#form-triagem", async (_, campos) => {
    campos.prioridade ||= null;
    campos.responsavelId = campos.responsavelId ? Number(campos.responsavelId) : null;
    campos.previsao ||= null;
    await api("/api/ordens/" + detalheAtual.ordem.id + "/triagem", "PUT", campos);
    await abrirDetalhe(detalheAtual.ordem.id, false);
    $("#retorno").textContent = "Triagem registrada.";
});

lidarFormulario("#form-mensagem", async (formulario, campos) => {
    await api("/api/ordens/" + detalheAtual.ordem.id + "/mensagens", "POST", campos);
    formulario.reset();
    await abrirDetalhe(detalheAtual.ordem.id, false);
    $("#retorno").textContent = "Mensagem registrada.";
});

lidarFormulario("#form-entrega", async (formulario, campos) => {
    await api("/api/ordens/" + detalheAtual.ordem.id + "/entregas", "POST", campos);
    formulario.reset();
    await abrirDetalhe(detalheAtual.ordem.id, false);
    $("#retorno").textContent = "Versão encaminhada para homologação.";
    $("#detalhe-titulo").focus();
});

lidarFormulario("#form-homologacao", async (formulario, campos) => {
    const rota = "/api/ordens/" + detalheAtual.ordem.id + "/entregas/" + Number(campos.entregaId) + "/homologacao";
    await api(rota, "POST", { resultado: campos.resultado, motivo: campos.motivo });
    formulario.reset();
    $("#motivo").required = false;
    await abrirDetalhe(detalheAtual.ordem.id, false);
    $("#retorno").textContent = campos.resultado === "Devolvida"
        ? "Versão devolvida para ajuste." : "Versão aprovada.";
    $("#detalhe-titulo").focus();
});

$("#resultado").addEventListener("change", () => { $("#motivo").required = $("#resultado").value === "Devolvida"; });
for (const seletor of ["#f-sistema", "#f-situacao", "#f-previsao"]) {
    $(seletor).addEventListener("change", renderizarLista);
}
for (const filtro of filtrosPessoas) {
    $("#limpar-" + filtro.campo).addEventListener("click", () => {
        filtro.selecao.clear();
        atualizarOpcoesPessoas(filtro);
        renderizarLista();
        $("#resumo-" + filtro.campo).parentElement.focus();
    });
}
$("#ver-lista").addEventListener("click", () => { kanban = false; renderizarLista(); });
$("#ver-kanban").addEventListener("click", () => { kanban = true; renderizarLista(); });
$("#abrir-nova").addEventListener("click", () => { $("#nova").hidden = false; $("#titulo-nova").focus(); });
$("#cancelar-nova").addEventListener("click", () => {
    $("#form-nova").reset();
    $("#nova").hidden = true;
    $("#abrir-nova").focus();
});
$("#atualizar").addEventListener("click", () => executar($("#atualizar"), atualizarLista));
function mostrarVisaoOrdens() {
    detalheAtual = null;
    $("#detalhe").hidden = true;
    $("#visao").hidden = false;
}

function irParaSecaoOrdens() {
    const secao = location.hash;
    if (!sessao || !["#nova", "#metricas", "#filtros-ordens", "#lista-ordens"].includes(secao))
        return;
    if ((secao === "#nova" && sessao.fornecedora) || ($("#visao").hidden && $("#detalhe").hidden))
        return;

    mostrarVisaoOrdens();
    if (secao === "#nova") $("#nova").hidden = false;
    const alvo = $(secao);
    alvo.focus({ preventScroll: true });
    alvo.scrollIntoView({ block: "start" });
}

window.addEventListener("hashchange", irParaSecaoOrdens);
for (const link of document.querySelectorAll(".submenu a")) {
    link.addEventListener("click", () => {
        if (link.hash === location.hash) irParaSecaoOrdens();
    });
}

function focarOrdemNaVisao(id) {
    const modo = sessao.fornecedora && kanban ? "kanban" : "lista";
    const alvo = document.getElementById("os-" + modo + "-" + id);
    (alvo || $("#titulo-pagina")).focus();
}

$("#voltar").addEventListener("click", () => executar($("#voltar"), async () => {
    const id = detalheAtual?.ordem.id;
    await atualizarLista();
    mostrarVisaoOrdens();
    focarOrdemNaVisao(id);
}));

(async function iniciar() {
    try {
        sessao = await api("/api/acesso/");
        $("#identidade").textContent = sessao.empresa + " · " + sessao.perfil;
        $("#abrir-nova").hidden = sessao.fornecedora;
        $("#modos").hidden = !sessao.fornecedora;
        opcoesSeletor($("#f-situacao"), Object.entries(estados), "Todas");

        opcoes = await api("/api/ordens/opcoes");
        if (!sessao.fornecedora)
            opcoesSeletor($("#sistema-novo"), opcoes.map(opcao => [String(opcao.id), opcao.nome]), "Selecione");
        const tiposMensagem = [["Mensagem", "Mensagem"]];
        if (sessao.fornecedora) tiposMensagem.push(["SolicitacaoVpn", "Solicitação de VPN"]);
        opcoesSeletor($("#tipo-mensagem"), tiposMensagem);

        await atualizarLista();
        $("#visao").hidden = false;
        irParaSecaoOrdens();
    } catch (falha) {
        erro(falha.message);
    }
})();
