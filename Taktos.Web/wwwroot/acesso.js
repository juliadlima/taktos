"use strict";

const formulario = document.querySelector("#formulario");
const mensagem = document.querySelector("#mensagem");
const email = document.querySelector("#email");
const senha = document.querySelector("#senha");
const entrar = document.querySelector("#entrar");

formulario.addEventListener("submit", async evento => {
    evento.preventDefault();
    mensagem.textContent = "";
    entrar.disabled = true;

    try {
        const resposta = await fetch("/api/acesso/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email: email.value, senha: senha.value })
        });

        if (resposta.ok) {
            location.replace("/ordens.html");
        } else if (resposta.status === 401) {
            mensagem.textContent = "E-mail ou senha inválidos.";
            senha.focus();
        } else if (resposta.status === 403) {
            const detalhe = await resposta.json().catch(() => null);
            mensagem.textContent = detalhe?.mensagem ?? "Seu acesso ainda não foi liberado.";
        } else if (resposta.status === 400) {
            mensagem.textContent = "Confira o e-mail e a senha informados.";
        } else {
            mensagem.textContent = "Não foi possível iniciar a sessão. Tente novamente.";
        }
    } catch {
        mensagem.textContent = "Não foi possível se comunicar com o portal. Tente novamente.";
    } finally {
        senha.value = "";
        entrar.disabled = false;
    }
});

formulario.hidden = false;
