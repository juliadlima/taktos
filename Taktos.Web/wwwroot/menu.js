"use strict";

(() => {
    const sair = document.querySelector("#sair");
    const mensagem = document.querySelector("#mensagem-menu");
    const links = document.querySelectorAll(".submenu a");

    function sinalizarSecao() {
        for (const link of links) {
            if (link.hash === location.hash)
                link.setAttribute("aria-current", "location");
            else
                link.removeAttribute("aria-current");
        }
    }

    window.addEventListener("hashchange", sinalizarSecao);
    sinalizarSecao();

    sair.addEventListener("click", async () => {
        mensagem.textContent = "";
        sair.disabled = true;

        try {
            const resposta = await fetch("/api/acesso/logout", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: "{}"
            });
            if (!resposta.ok && resposta.status !== 401)
                throw new Error("Falha ao encerrar a sessão.");

            location.replace("/");
        } catch {
            mensagem.textContent = "Não foi possível sair. Tente novamente.";
        } finally {
            sair.disabled = false;
        }
    });
})();
