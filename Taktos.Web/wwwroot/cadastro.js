"use strict";

const achar = seletor => document.querySelector(seletor);
const formularioConta = achar("#criar-conta");
const listaAprovacoes = achar("#lista-aprovacoes");
const formularioPerfil = achar("#form-perfil");
const diretorio = achar("#diretorio-usuarios");
const listaEmpresas = achar("#lista-empresas");
const listaEquipe = achar("#lista-equipe-fornecedora");
const mensagem = achar("#mensagem");

function apresentarErro(erro) {
    mensagem.textContent = erro instanceof TypeError
        ? "Não foi possível conectar ao Taktos. Tente novamente."
        : erro.message;
}

async function requisitar(rota, metodo = "GET", dados) {
    const opcoes = { method: metodo };
    if (dados !== undefined) {
        opcoes.headers = { "Content-Type": "application/json" };
        opcoes.body = JSON.stringify(dados);
    }

    const resposta = await fetch(rota, opcoes);
    if (resposta.status === 401 && !formularioConta) {
        location.replace("/login.html");
        throw new Error("Entre novamente para acessar o Taktos.");
    }
    if (!resposta.ok) {
        const erro = await resposta.json().catch(() => null);
        const falha = new Error(erro?.mensagem || "Não foi possível concluir a solicitação. Confira os dados e tente novamente.");
        falha.campo = erro?.campo;
        falha.status = resposta.status;
        throw falha;
    }

    return resposta.status === 204 ? null : resposta.json();
}

function aoEnviar(formulario, executar) {
    formulario.addEventListener("submit", async evento => {
        evento.preventDefault();
        mensagem.textContent = "";
        const botao = formulario.querySelector('button[type="submit"]');
        botao.disabled = true;

        try {
            await executar(Object.fromEntries(new FormData(formulario)));
        } catch (erro) {
            if (erro.campo) globalThis.taktosCampos?.marcarErro(formulario, erro.campo, erro.message);
            apresentarErro(erro);
        } finally {
            botao.disabled = false;
        }
    });
}

function aoAcionar(botao, executar) {
    botao.addEventListener("click", async () => {
        mensagem.textContent = "";
        botao.disabled = true;

        try {
            await executar();
        } catch (erro) {
            apresentarErro(erro);
        } finally {
            botao.disabled = false;
        }
    });
}

function campoEdicao(formulario, id, nome, rotulo, tipo, limite, requerido, valor = "") {
    const bloco = elemento("p");
    const label = elemento("label", rotulo);
    const campo = elemento("input");
    label.htmlFor = campo.id = id;
    campo.name = nome;
    campo.type = tipo;
    campo.maxLength = limite;
    campo.required = requerido;
    campo.value = valor;
    bloco.append(label, campo);
    formulario.append(bloco);
    return campo;
}

function elemento(tag, texto, classe) {
    const no = document.createElement(tag);
    if (texto !== undefined) no.textContent = texto;
    if (classe) no.className = classe;
    return no;
}

function montarConta(conta, rota) {
    const retorno = achar("#retorno");
    const item = elemento("article", undefined, "linha-item");
    const titulo = elemento("h3", conta.nome);
    const identificacao = elemento("p", conta.empresa + " · " + conta.email, "apoio");
    if (conta.administrador) {
        const perfil = elemento("a", "Meu perfil");
        perfil.href = "/perfil.html";
        item.append(titulo, identificacao, elemento("p", "Conta administradora", "apoio"), perfil);
        return item;
    }

    const status = elemento("p", undefined, "apoio");
    const botaoSituacao = elemento("button", undefined, "secundario");
    botaoSituacao.type = "button";

    function mostrarSituacao() {
        const acao = conta.ativo ? "Desativar conta" : "Reativar conta";
        status.textContent = (conta.ativo ? "Conta ativa" : "Conta desativada") +
            (conta.empresaAtiva === false ? " · Empresa desativada" : "");
        botaoSituacao.textContent = acao;
        botaoSituacao.setAttribute("aria-label", acao + " de " + conta.nome);
    }

    const editor = elemento("details");
    const formEdicao = elemento("form");
    formEdicao.method = "post";
    const nome = campoEdicao(formEdicao, "conta-nome-" + conta.id, "nome", "Nome completo",
        "text", 120, true, conta.nome);
    const email = campoEdicao(formEdicao, "conta-email-" + conta.id, "email", "E-mail",
        "email", 254, true, conta.email);
    const senha = campoEdicao(formEdicao, "conta-senha-" + conta.id, "senhaNova", "Redefinir senha (opcional)",
        "password", 128, false);
    senha.minLength = 12;
    senha.autocomplete = "new-password";
    const salvar = elemento("button", "Salvar alterações");
    salvar.type = "submit";
    formEdicao.append(salvar);
    const abrirEditor = elemento("summary", "Editar conta");
    editor.append(abrirEditor, formEdicao);
    item.append(titulo, identificacao, status, editor, botaoSituacao);

    aoEnviar(formEdicao, async campos => {
        retorno.textContent = "";
        await requisitar(rota, "PUT", { nome: campos.nome, email: campos.email, senhaNova: campos.senhaNova });
        conta.nome = nome.value = campos.nome.trim();
        conta.email = email.value = campos.email.trim().toLowerCase();
        senha.value = "";
        titulo.textContent = conta.nome;
        identificacao.textContent = conta.empresa + " · " + conta.email;
        mostrarSituacao();
        editor.open = false;
        abrirEditor.focus();
        retorno.textContent = "Conta de " + conta.nome + " atualizada.";
    });

    aoAcionar(botaoSituacao, async () => {
        retorno.textContent = "";
        const ativo = !conta.ativo;
        await requisitar(rota + "/situacao", "PUT", { ativo });
        conta.ativo = ativo;
        mostrarSituacao();
        retorno.textContent = "Conta de " + conta.nome + (ativo ? " reativada." : " desativada.");
    });

    mostrarSituacao();
    return item;
}

if (formularioConta) {
    aoEnviar(formularioConta, async campos => {
        if (campos.senha !== campos.confirmacao) {
            achar("#cad-confirmacao").focus();
            const falha = new Error("As senhas informadas devem ser iguais.");
            falha.campo = "confirmacao";
            throw falha;
        }

        await requisitar("/api/cadastro", "POST", {
            nome: campos.nome, email: campos.email, empresa: campos.empresa, senha: campos.senha
        });
        formularioConta.reset();
        formularioConta.hidden = true;
        achar("#cad-sucesso").hidden = false;
        achar("#cad-concluido").focus();
    });
}

if (listaAprovacoes) {
    const atualizar = achar("#atualizar-aprovacoes");
    const retorno = achar("#retorno");
    const horario = new Intl.DateTimeFormat("pt-BR", {
        dateStyle: "short", timeStyle: "short", timeZone: "America/Sao_Paulo"
    });

    async function carregarAprovacoes() {
        atualizar.disabled = true;
        listaAprovacoes.setAttribute("aria-busy", "true");

        try {
            const solicitacoes = await requisitar("/api/cadastro/solicitacoes");
            listaAprovacoes.replaceChildren();
            if (solicitacoes.length === 0)
                listaAprovacoes.append(elemento("p", "Nenhuma solicitação pendente.", "apoio"));

            for (const pedido of solicitacoes) {
                const item = elemento("article", undefined, "linha-item");
                item.append(
                    elemento("h3", pedido.nome),
                    elemento("p", pedido.empresa + " · " + pedido.email),
                    elemento("p", "Solicitada em " + horario.format(new Date(pedido.solicitadaEm)), "apoio")
                );
                const acoes = elemento("div", undefined, "acoes");
                const aprovar = elemento("button", "Aprovar acesso");
                const suspensa = pedido.empresaAtiva === false;
                aprovar.disabled = suspensa;
                if (suspensa)
                    item.append(elemento("p", "Reative a empresa para aprovar este acesso.", "apoio"));
                const recusar = elemento("button", "Recusar", "secundario");

                async function decidir(decisao) {
                    mensagem.textContent = "";
                    aprovar.disabled = recusar.disabled = true;
                    try {
                        await requisitar("/api/cadastro/solicitacoes/" + pedido.id + "/decisao", "POST",
                            { aprovar: decisao });
                        item.remove();
                        if (!listaAprovacoes.firstElementChild)
                            listaAprovacoes.append(elemento("p", "Nenhuma solicitação pendente.", "apoio"));
                        retorno.textContent = decisao ? "Acesso aprovado para " + pedido.nome + "." :
                            "Solicitação recusada para " + pedido.nome + ".";
                        atualizar.focus();
                        if (decisao) carregarContas();
                    } catch (erro) {
                        apresentarErro(erro);
                    } finally {
                        aprovar.disabled = suspensa;
                        recusar.disabled = false;
                    }
                }

                aprovar.type = recusar.type = "button";
                aprovar.addEventListener("click", () => decidir(true));
                recusar.addEventListener("click", () => decidir(false));
                acoes.append(aprovar, recusar);
                item.append(acoes);
                listaAprovacoes.append(item);
            }
        } catch (erro) {
            apresentarErro(erro);
        } finally {
            listaAprovacoes.setAttribute("aria-busy", "false");
            atualizar.disabled = false;
        }
    }

    const listaContas = achar("#lista-contas");
    const formManual = achar("#form-conta-manual");
    const empresaManual = achar("#manual-empresa");
    let revisaoContas = 0;

    async function carregarEmpresasAtivas() {
        try {
            const empresas = await requisitar("/api/empresas");
            const ativas = empresas.filter(empresa => empresa.ativa);
            const atual = empresaManual.value;
            empresaManual.replaceChildren(new Option("Selecione", ""));
            for (const empresa of ativas)
                empresaManual.append(new Option(empresa.nome, String(empresa.id)));
            empresaManual.value = ativas.some(empresa => String(empresa.id) === atual) ? atual : "";
            formManual.hidden = ativas.length === 0;
            achar("#sem-empresa-ativa").hidden = ativas.length > 0;
        } catch (erro) {
            apresentarErro(erro);
        }
    }

    async function carregarContas() {
        const revisao = ++revisaoContas;
        listaContas.setAttribute("aria-busy", "true");
        try {
            const contas = await requisitar("/api/cadastro/contas");
            if (revisao !== revisaoContas) return;

            listaContas.replaceChildren();
            if (contas.length === 0)
                listaContas.append(elemento("p", "Nenhuma conta cliente cadastrada.", "apoio"));

            for (const conta of contas)
                listaContas.append(montarConta(conta, "/api/cadastro/contas/" + conta.id));
        } catch (erro) {
            if (revisao === revisaoContas) apresentarErro(erro);
        } finally {
            if (revisao === revisaoContas) listaContas.setAttribute("aria-busy", "false");
        }
    }

    aoEnviar(formManual, async campos => {
        retorno.textContent = "";
        await requisitar("/api/cadastro/contas", "POST", {
            empresaId: Number(campos.empresaId), nome: campos.nome, email: campos.email, senha: campos.senha
        });
        formManual.reset();
        retorno.textContent = "Conta de " + campos.nome.trim() + " criada com acesso liberado.";
        await carregarContas();
    });

    atualizar.addEventListener("click", () => {
        mensagem.textContent = "";
        carregarAprovacoes();
        carregarContas();
        carregarEmpresasAtivas();
    });
    carregarAprovacoes();
    carregarContas();
    carregarEmpresasAtivas();
}

if (formularioPerfil) {
    const formIdentidade = achar("#form-identidade");

    function preencherIdentidade(empresa, perfil) {
        achar("#perfil-empresa").textContent = empresa;
        achar("#perfil-papel").textContent = perfil;
    }

    function preencherPerfil(perfil) {
        preencherIdentidade(perfil.empresa, perfil.perfil);
        achar("#perfil-nome").value = perfil.nome;
        achar("#perfil-email").value = perfil.email;
    }

    requisitar("/api/acesso/perfil").then(perfil => {
        preencherPerfil(perfil);
        formularioPerfil.hidden = false;
        if (location.hash === "#seguranca-perfil")
            achar("#seguranca-perfil").scrollIntoView({ block: "start" });
        if (formIdentidade) {
            achar("#identidade-empresa").value = perfil.empresa;
            achar("#identidade-perfil").value = perfil.perfil;
            formIdentidade.hidden = false;
        }
    }).catch(apresentarErro);

    if (formIdentidade) {
        const retornoIdentidade = achar("#retorno-identidade");
        aoEnviar(formIdentidade, async campos => {
            retornoIdentidade.textContent = "";
            const identidade = await requisitar("/api/equipe/identidade", "PUT", {
                empresa: campos.empresa, perfil: campos.perfil
            });
            preencherIdentidade(identidade.empresa, identidade.perfil);
            achar("#identidade-empresa").value = identidade.empresa;
            achar("#identidade-perfil").value = identidade.perfil;
            retornoIdentidade.textContent = "Nomes da fornecedora atualizados.";
        });
    }

    aoEnviar(formularioPerfil, async campos => {
        achar("#retorno").textContent = "";
        if (campos.senhaNova !== campos.confirmacao) {
            achar("#perfil-confirmacao").focus();
            throw new Error("As senhas informadas devem ser iguais.");
        }

        const perfil = await requisitar("/api/acesso/perfil", "PUT", {
            nome: campos.nome, email: campos.email, senhaAtual: campos.senhaAtual, senhaNova: campos.senhaNova
        });
        for (const campo of ["#perfil-senha-atual", "#perfil-senha-nova", "#perfil-confirmacao"])
            achar(campo).value = "";
        preencherPerfil(perfil);
        achar("#retorno").textContent = "Perfil atualizado.";
    });
}

if (listaEquipe) {
    const formulario = achar("#form-usuario-equipe");
    const rota = "/api/equipe/usuarios";
    const contaAdministradora = achar("#conta-administradora");

    async function carregarEquipe() {
        listaEquipe.setAttribute("aria-busy", "true");
        contaAdministradora.setAttribute("aria-busy", "true");
        try {
            const contas = await requisitar(rota);
            const administradora = contas.find(conta => conta.administrador);
            const equipe = contas.filter(conta => !conta.administrador);
            contaAdministradora.replaceChildren(administradora
                ? montarConta(administradora, rota + "/" + administradora.id)
                : elemento("p", "Conta administradora indisponível.", "apoio"));
            listaEquipe.replaceChildren(...equipe.map(conta => montarConta(conta, rota + "/" + conta.id)));
            if (equipe.length === 0)
                listaEquipe.append(elemento("p", "Nenhum outro usuário cadastrado.", "apoio"));
        } catch (erro) {
            apresentarErro(erro);
        } finally {
            listaEquipe.setAttribute("aria-busy", "false");
            contaAdministradora.setAttribute("aria-busy", "false");
        }
    }

    aoEnviar(formulario, async campos => {
        achar("#retorno").textContent = "";
        await requisitar(rota, "POST", { nome: campos.nome, email: campos.email, senha: campos.senha });
        formulario.reset();
        achar("#retorno").textContent = "Usuário " + campos.nome.trim() + " cadastrado.";
        await carregarEquipe();
    });
    carregarEquipe();
}

if (diretorio) {
    requisitar("/api/acesso/usuarios").then(equipe => {
        achar("#usuarios-empresa").textContent = equipe.empresa;
        const total = equipe.usuarios.length;
        achar("#usuarios-contagem").textContent = total + (total === 1 ? " usuário" : " usuários");
        const lista = achar("#usuarios-lista");
        for (const usuario of equipe.usuarios) {
            const item = elemento("li", undefined, "linha-item");
            item.append(elemento("strong", usuario.nome), elemento("span", usuario.email, "apoio"));
            lista.append(item);
        }
    }).catch(apresentarErro).finally(() => diretorio.setAttribute("aria-busy", "false"));
}

if (listaEmpresas) {
    const formulario = achar("#form-empresa");
    const retorno = achar("#retorno");
    const vazio = elemento("p", "Nenhuma empresa cadastrada.", "apoio");

    function exibirEmpresa(empresa) {
        const item = elemento("article", undefined, "linha-item");
        const titulo = elemento("h3", empresa.nome);
        titulo.tabIndex = -1;
        const codigo = elemento("p", undefined, "apoio");
        codigo.append(elemento("span", "Código da empresa: "), elemento("code", empresa.codigo));
        const status = elemento("p", undefined, "apoio");
        const alterarSituacao = elemento("button", undefined, "secundario");
        alterarSituacao.type = "button";
        function mostrarSituacao() {
            status.textContent = empresa.ativa ? "Empresa ativa" : "Empresa desativada";
            const acao = empresa.ativa ? "Desativar empresa" : "Reativar empresa";
            alterarSituacao.textContent = acao;
            alterarSituacao.setAttribute("aria-label", acao + " " + empresa.nome);
        }
        mostrarSituacao();
        item.append(titulo, codigo, status);

        const nenhumSistema = elemento("p", "Nenhum sistema cadastrado para esta empresa.", "apoio");
        const sistemas = elemento("ul", undefined, "sistemas-empresa");
        sistemas.setAttribute("aria-label", "Sistemas de " + empresa.nome);
        nenhumSistema.hidden = empresa.sistemas.length > 0;
        for (const sistema of empresa.sistemas)
            sistemas.append(elemento("li", sistema.nome));
        item.append(nenhumSistema, sistemas);

        const detalhes = elemento("details");
        const formSistema = elemento("form");
        const rotulo = elemento("label", "Nome do sistema");
        const entrada = elemento("input");
        entrada.id = "nome-sistema-" + empresa.id;
        entrada.name = "nome";
        entrada.maxLength = 100;
        entrada.required = true;
        rotulo.htmlFor = entrada.id;
        const salvar = elemento("button", "Adicionar sistema");
        salvar.type = "submit";
        const campos = elemento("div", undefined, "form-empresa");
        campos.append(entrada, salvar);
        formSistema.append(rotulo, campos);
        detalhes.append(elemento("summary", "Adicionar sistema"), formSistema);
        const editor = elemento("details");
        const abrirEditor = elemento("summary", "Editar empresa");
        const formEdicao = elemento("form");
        formEdicao.method = "post";
        const nome = campoEdicao(formEdicao, "editar-empresa-" + empresa.id, "nome", "Nome da empresa",
            "text", 120, true, empresa.nome);
        const salvarEdicao = elemento("button", "Salvar nome");
        salvarEdicao.type = "submit";
        formEdicao.append(salvarEdicao);
        editor.append(abrirEditor, formEdicao);
        item.append(detalhes, editor, alterarSituacao);

        const rota = "/api/empresas/" + empresa.id;
        aoEnviar(formEdicao, async campos => {
            retorno.textContent = "";
            await requisitar(rota, "PUT", { nome: campos.nome });
            empresa.nome = nome.value = campos.nome.trim();
            titulo.textContent = empresa.nome;
            sistemas.setAttribute("aria-label", "Sistemas de " + empresa.nome);
            mostrarSituacao();
            editor.open = false;
            abrirEditor.focus();
            retorno.textContent = "Empresa " + empresa.nome + " atualizada.";
        });

        aoAcionar(alterarSituacao, async () => {
            retorno.textContent = "";
            const ativa = !empresa.ativa;
            await requisitar(rota + "/situacao", "PUT", { ativa });
            empresa.ativa = ativa;
            mostrarSituacao();
            retorno.textContent = "Empresa " + empresa.nome + (ativa ? " reativada." : " desativada.");
        });

        aoEnviar(formSistema, async dados => {
            retorno.textContent = "";
            const sistema = await requisitar(rota + "/sistemas", "POST",
                { nome: dados.nome });
            sistemas.append(elemento("li", sistema.nome));
            nenhumSistema.hidden = true;
            formSistema.reset();
            retorno.textContent = "Sistema adicionado em " + empresa.nome + ".";
            entrada.focus();
        });

        listaEmpresas.append(item);
        return titulo;
    }

    requisitar("/api/empresas").then(empresas => {
        vazio.hidden = empresas.length > 0;
        listaEmpresas.append(vazio);
        for (const empresa of empresas)
            exibirEmpresa(empresa);
        formulario.hidden = false;
    }).catch(apresentarErro).finally(() => listaEmpresas.setAttribute("aria-busy", "false"));

    aoEnviar(formulario, async campos => {
        retorno.textContent = "";
        const empresa = await requisitar("/api/empresas", "POST", { nome: campos.nome });
        vazio.hidden = true;
        const titulo = exibirEmpresa(empresa);
        formulario.reset();
        retorno.textContent = "Empresa " + empresa.nome + " cadastrada.";
        titulo.focus();
    });
}

const formIndicador = achar("#form-indicador");
if (formIndicador) {
    const lista = achar("#lista-indicadores");
    const retorno = achar("#retorno");
    const seletorCriterio = achar("#indicador-criterio");
    const criterios = new Map([...seletorCriterio.options].map(opcao => [opcao.value, opcao.textContent]));

    function montarIndicador(indicador) {
        const item = elemento("li", undefined, "linha-item");
        const resumo = elemento("div", undefined, "resumo-indicador");
        const titulo = elemento("strong", indicador.nome);
        const contagem = elemento("span", criterios.get(indicador.criterio), "apoio");
        const tipo = elemento("span", indicador.padrao ? "Padrão" : "Personalizado", "tag");
        resumo.append(titulo, contagem, tipo);

        const editor = elemento("details");
        const abrir = elemento("summary", "Editar");
        abrir.setAttribute("aria-label", "Editar indicador " + indicador.nome);
        const formulario = elemento("form");
        formulario.method = "post";
        const nome = campoEdicao(formulario, "indicador-nome-" + indicador.id, "nome",
            "Nome do cartão", "text", 120, true, indicador.nome);
        const campo = elemento("p");
        const criterio = seletorCriterio.cloneNode(true);
        criterio.id = "indicador-criterio-" + indicador.id;
        criterio.value = indicador.criterio;
        const rotulo = elemento("label", "O que contar");
        rotulo.htmlFor = criterio.id;
        campo.append(rotulo, criterio);
        const salvar = elemento("button", "Salvar alterações");
        salvar.type = "submit";
        formulario.append(campo, salvar);
        editor.append(abrir, formulario);
        item.append(resumo, editor);

        aoEnviar(formulario, async campos => {
            retorno.textContent = "";
            const atualizado = await requisitar("/api/indicadores/" + indicador.id, "PUT",
                { nome: campos.nome, criterio: campos.criterio });
            titulo.textContent = nome.value = atualizado.nome;
            criterio.value = atualizado.criterio;
            contagem.textContent = criterios.get(atualizado.criterio);
            abrir.setAttribute("aria-label", "Editar indicador " + atualizado.nome);
            editor.open = false;
            abrir.focus();
            retorno.textContent = "Indicador " + atualizado.nome + " atualizado.";
        });

        return item;
    }

    requisitar("/api/indicadores").then(indicadores => {
        lista.replaceChildren(...indicadores.map(montarIndicador));
        formIndicador.hidden = false;
    }).catch(apresentarErro).finally(() => lista.setAttribute("aria-busy", "false"));

    aoEnviar(formIndicador, async campos => {
        retorno.textContent = "";
        const cadastrado = await requisitar("/api/indicadores", "POST", { nome: campos.nome, criterio: campos.criterio });
        lista.append(montarIndicador(cadastrado));
        formIndicador.reset();
        retorno.textContent = "Indicador " + cadastrado.nome + " cadastrado.";
    });
}

const formRelato = achar("#form-relato");
if (formRelato) {
    const lista = achar("#lista-relatos");
    const retorno = achar("#retorno");
    const formatoData = new Intl.DateTimeFormat("pt-BR", {
        dateStyle: "short", timeStyle: "short", timeZone: "America/Sao_Paulo"
    });
    const categorias = { Acessibilidade: "Barreira de acessibilidade", Discriminacao: "Discriminação ou desrespeito" };
    const situacoes = { Recebido: "Recebido", EmAnalise: "Em análise", Respondido: "Respondido" };

    function montarRelato(relato) {
        const item = elemento("article", undefined, "linha-item");
        const titulo = elemento("h3", "Relato " + relato.id + " · " + categorias[relato.categoria]);
        titulo.id = "relato-" + relato.id;
        titulo.tabIndex = -1;
        const data = formatoData.format(new Date(relato.criadoEm));
        const origem = relato.meu ? "Meu relato" : "Relato recebido";
        item.append(titulo, elemento("p", origem + " · " + data, "apoio"),
            elemento("p", "Situação: " + situacoes[relato.situacao]));
        if (relato.ordemId) item.append(elemento("p", "OS vinculada: " + relato.ordemId, "apoio"));
        item.append(elemento("p", relato.descricao, "quebra"));

        if (relato.resposta) {
            item.append(elemento("h4", "Resposta"), elemento("p", relato.resposta, "quebra"));
        }

        if (relato.podeTratar && relato.situacao !== "Respondido") {
            const rota = "/api/relatos/" + relato.id + "/situacao";
            async function tratar(situacao, resposta) {
                await requisitar(rota, "PUT", { situacao, resposta });
                retorno.textContent = situacao === "Respondido" ? "Resposta enviada." : "Relato em análise.";
                await carregarRelatos();
                achar("#" + titulo.id)?.focus();
            }

            if (relato.situacao === "Recebido") {
                const analisar = elemento("button", "Iniciar análise", "secundario");
                analisar.type = "button";
                aoAcionar(analisar, () => tratar("EmAnalise", null));
                item.append(analisar);
            }

            const editor = elemento("details");
            const form = elemento("form");
            form.method = "post";
            const resposta = elemento("textarea");
            resposta.id = "resposta-relato-" + relato.id;
            resposta.name = "resposta";
            resposta.required = true;
            resposta.maxLength = 2000;
            const rotulo = elemento("label", "Resposta do atendimento");
            rotulo.htmlFor = resposta.id;
            const enviar = elemento("button", "Enviar resposta");
            enviar.type = "submit";
            form.append(rotulo, resposta, enviar);
            aoEnviar(form, campos => tratar("Respondido", campos.resposta));
            editor.append(elemento("summary", "Responder ao relato"), form);
            item.append(editor);
        }

        return item;
    }

    async function carregarRelatos() {
        lista.setAttribute("aria-busy", "true");
        try {
            const relatos = await requisitar("/api/relatos");
            lista.replaceChildren(...relatos.map(montarRelato));
            if (relatos.length === 0) lista.append(elemento("p", "Nenhum relato para acompanhar.", "apoio"));
        } catch (erro) {
            apresentarErro(erro);
        } finally {
            lista.setAttribute("aria-busy", "false");
        }
    }

    aoEnviar(formRelato, async campos => {
        retorno.textContent = "";
        const ordemId = campos.ordemId ? Number(campos.ordemId) : null;
        let registro;
        try {
            registro = await requisitar("/api/relatos", "POST", {
                categoria: campos.categoria, descricao: campos.descricao, ordemId
            });
        } catch (erro) {
            if (ordemId && erro.status === 404) erro.campo = "ordemId";
            throw erro;
        }
        formRelato.reset();
        retorno.textContent = "Relato " + registro.id + " registrado. Acompanhe o retorno nesta página.";
        await carregarRelatos();
    });

    aoAcionar(achar("#atualizar-relatos"), carregarRelatos);
    carregarRelatos();
}
