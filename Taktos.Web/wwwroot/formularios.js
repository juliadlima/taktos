"use strict";

(() => {
    let proximoId = 0;

    function idErro(campo) {
        return campo.id + "-erro";
    }

    function marcar(campo, texto) {
        if (!(campo instanceof HTMLElement)) return false;
        if (!campo.id) campo.id = "campo-formulario-" + (++proximoId);
        let detalhe = document.getElementById(idErro(campo));
        if (!detalhe) {
            detalhe = document.createElement("small");
            detalhe.id = idErro(campo);
            detalhe.className = "erro-campo";
            campo.after(detalhe);
        }

        detalhe.textContent = texto;
        const referencias = (campo.getAttribute("aria-describedby") || "").split(/\s+/).filter(Boolean);
        if (!referencias.includes(detalhe.id)) referencias.push(detalhe.id);
        campo.setAttribute("aria-describedby", referencias.join(" "));
        campo.setAttribute("aria-invalid", "true");
        return true;
    }

    function limpar(campo) {
        if (!(campo instanceof HTMLElement) || !campo.id) return;
        const detalhe = document.getElementById(idErro(campo));
        if (!detalhe) return;
        detalhe.remove();
        campo.removeAttribute("aria-invalid");
        const referencias = (campo.getAttribute("aria-describedby") || "").split(/\s+/)
            .filter(referencia => referencia && referencia !== detalhe.id);
        if (referencias.length) campo.setAttribute("aria-describedby", referencias.join(" "));
        else campo.removeAttribute("aria-describedby");
    }

    function descreverErro(campo) {
        if (campo.validity.valueMissing) return "Preencha este campo obrigatório.";
        if (campo.validity.typeMismatch && campo.type === "email") return "Informe um e-mail válido, como nome@empresa.com.";
        if (campo.validity.tooShort) return "Use pelo menos " + campo.minLength + " caracteres.";
        if (campo.validity.tooLong) return "Use até " + campo.maxLength + " caracteres.";
        return campo.validationMessage;
    }

    document.addEventListener("invalid", evento => marcar(evento.target, descreverErro(evento.target)), true);
    for (const tipo of ["input", "change"])
        document.addEventListener(tipo, evento => limpar(evento.target));
    document.addEventListener("reset", evento => {
        for (const campo of evento.target.elements) limpar(campo);
    });

    globalThis.taktosCampos = {
        marcarErro(formulario, campo, texto) {
            const destino = formulario.elements.namedItem(campo);
            if (marcar(destino, texto)) destino.focus();
        }
    };
})();
