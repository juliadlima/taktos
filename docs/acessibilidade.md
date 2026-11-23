# Etapa 3 do PIM — Responsabilidade social, diversidade e acessibilidade

| Exigência | Ação implementada no Taktos |
|---|---|
| Combate à discriminação | Registro de discriminação ou desrespeito em **Ajuda e acessibilidade**, com resposta reservada. |
| Valorização da diversidade | Guia em linguagem direta com instruções de primeiro acesso, orientações de convivência e glossário dos termos de atendimento. |
| Recursos tecnológicos inclusivos | Rótulos associados aos campos, identificação textual de erros, anúncios de resultados e mudanças de situação na web e títulos semânticos no Android. |
| Ações voltadas à acessibilidade | Atalho para o conteúdo, foco visível e reposicionado nas ordens, bordas de maior contraste e visão em lista inicial em telas estreitas. |
| Tecnologia para promoção da cidadania | Guia público e canal para informar barreiras, consultar situação e receber retorno. |

O Consultor ADM da fornecedora vinculada é a conta que recebe e trata os relatos. O autor pode consultar a própria ocorrência e a resposta. Colegas do cliente e integrantes da fornecedora com perfil comum não têm acesso. O formulário admite uma OS opcional, desde que esteja no escopo de consulta do autor. Para testar, use dados fictícios.

## Roteiro de avaliação manual

Registre a data, o navegador ou dispositivo, a conta de demonstração e o resultado observado. Use capturas ou gravações com dados fictícios para demonstrar as etapas.

| Cenário | Como testar | Resultado esperado |
|---|---|---|
| Atalho e teclado | Abra a página inicial e pressione Tab. Ative **Pular para o conteúdo principal**. Entre como cliente e abra uma OS usando Tab, Shift+Tab, Enter e Espaço. | O atalho aparece no foco e leva ao conteúdo. O foco é visível, segue uma ordem lógica e permite abrir a OS. |
| Resultado dos filtros | Abra **Ordens de serviço** com o NVDA no Windows. Marque solicitantes e responsáveis. | O leitor de tela identifica o grupo e suas opções e anuncia a quantidade de ordens encontradas. |
| Validação e resposta | Abra uma versão para homologação. Escolha **Devolvida** e tente enviar sem motivo. Depois preencha e envie. | O campo **Motivo** é identificado. A confirmação anuncia que a versão foi devolvida para ajuste. |
| Ampliação e tela estreita | Repita uma consulta com texto ampliado a 200% e em uma janela com 320 pixels CSS de largura. | Menu, botões, filtros e campos continuam acessíveis. Em tela estreita a visão inicial usa lista. |
| Contraste | Use um verificador de contraste para campos, links, botão em foco e rótulos de situação. | Observe os critérios de contraste de texto e de componentes, conforme a função do elemento. |
| Relato reservado | Envie um relato como TI do Cliente Alfa. Entre como um colega, como outra empresa e como Consultor ADM. Responda e volte à conta autora. | Apenas o autor e o Consultor ADM responsável veem o relato. O autor consulta o retorno na mesma página. |
| Android | Abra a lista e um detalhe com TalkBack. Verifique a navegação por títulos e o texto no tamanho ampliado do sistema. | TalkBack identifica os títulos e permite consultar a OS e as versões. |

## Referências

- [WCAG 2.2, W3C](https://www.w3.org/TR/WCAG22/)
- [Checklist de apoio à ABNT NBR 17225, Academia de Acessibilidade](https://academiadeacessibilidade.com.br/ferramentas/checklist-abnt/index.html)
- [WCAG2ICT para software e aplicativos nativos, W3C](https://www.w3.org/WAI/standards-guidelines/wcag/non-web-ict/)
