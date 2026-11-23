# Roteiro de demonstração

Use as contas fictícias preparadas no README. Saia entre os perfis ou use janelas separadas. Registre o resultado observado em cada passo e as evidências do ambiente executado.

## Navegação inicial do portal

1. Em uma janela anônima, abra a página inicial e clique em **Acessar o portal**. A tela de login deve aparecer.
2. Entre com a conta da fornecedora ou do Cliente Alfa. A página **Ordens de serviço** deve abrir. Em outra tela interna, clique na marca **Taktos** e confirme o retorno às ordens.
3. Clique em **Sair** e confirme que a página inicial para visitantes volta a aparecer.

## Fluxo das sprints

| Sprint | Passos | Resultado esperado |
|---|---|---|
| 1 | Entre como `ti.alfa@portal.test`. Abra uma OS de correção do protocolo de entrega de carga no REDEX. Consulte como Beta e depois como fornecedora. Faça a triagem e informe a previsão. | Alfa abre e consulta a OS, Beta consulta as próprias OSs, e a fornecedora consegue atualizar a demanda de Alfa. A data passa a aparecer para Alfa. |
| 2 | No Kanban, mova a OS pelo botão da próxima situação. Na triagem, defina prioridade e responsável antes de iniciar a execução. Registre uma mensagem do tipo Solicitação de VPN. Entre como Alfa e responda. | A OS muda de coluna, as mensagens aparecem em ordem e a solicitação de VPN é identificada pelo texto. Teste os botões por teclado. |
| 3 | Como fornecedora, envie a versão 1.0.0 com a referência da GMUD. Como Alfa, devolva a entrega com um motivo. Registre 1.0.1 na mesma OS e aprove. Filtre a lista por sistema e situação. | O histórico preserva as duas versões. A primeira consta como devolvida, a segunda como aprovada e a OS como concluída. A visão consolidada calcula os indicadores sobre as OSs que aparecem no filtro. |
| 4 | Entre no aplicativo Android e abra a mesma OS. Use Atualizar depois de fazer uma alteração na web. Execute a validação em Docker se for apresentar a implantação. | O aplicativo consulta a situação, a previsão e as versões registradas pela web. A API responde às verificações de saúde e o pipeline cobre o fluxo de integração. |

## Critérios e indicadores

- Aguardando previsão é exibido quando a data ainda não foi registrada, inclusive quando a situação da OS já é Em análise.
- A aprovação e a devolução são registradas para uma versão específica. A devolução exige motivo.
- As OSs sem previsão são contadas entre as ordens em aberto. Entregas no prazo comparam a data da primeira entrega com a primeira previsão registrada, usando o horário de São Paulo. Versões devolvidas são somadas entre as ordens exibidas. Os três cartões mostram quantidades.

## Cadastro de uma empresa cliente

1. Entre como fornecedora, abra **Empresas** e cadastre uma empresa fictícia. Confira o código gerado e adicione **REDEX** como sistema atendido.
2. Na tela pública de login, selecione **Criar conta**, cadastre um e-mail de teste e informe o código da nova empresa. Entre como fornecedora, localize o pedido em **Aprovações de cadastro** e aprove o acesso.
3. Entre com a conta criada. Abra uma OS e confira se **REDEX** aparece no campo **Sistema**. Use a conta do Cliente Beta para conferir se a OS da empresa recém-cadastrada fica fora da lista.

## Cadastro, perfil e acesso por empresa

1. No login, clique em **Criar conta**, preencha o formulário com um e-mail novo e informe **Cliente Alfa**. Tente entrar com esse e-mail. O portal deve informar que o cadastro aguarda aprovação.
2. Entre como fornecedora, abra **Aprovações de cadastro** e aprove a solicitação. Saia e entre com o e-mail cadastrado. Em **Usuários da empresa**, confira se o novo usuário e a TI de Alfa aparecem. Em **Ordens de serviço**, confira se a OS do roteiro anterior está no painel.
3. Entre como TI do Cliente Beta. Confira se a nova conta de Alfa e a OS de Alfa ficam fora das listas.
4. Na conta criada, abra **Meu perfil**, altere o nome e salve. Em seguida, mude o e-mail e a senha, informando a senha atual. Saia e confirme que o login funciona com os novos dados.

## Edição e desativação

1. Na conta fornecedora, altere o nome da empresa de teste em **Empresas** e confirme que o código continua válido.
2. Em **Aprovações de cadastro**, crie uma conta manual para a empresa. Entre com o e-mail e a senha inicial. Edite o nome e o e-mail da conta pela fornecedora e teste as credenciais atualizadas.
3. Com uma sessão cliente aberta em outra janela, desative a conta. Atualize a página do cliente e tente entrar de novo. A sessão deve terminar e o login deve indicar que a conta está desativada. Confira que outro usuário da mesma empresa continua com acesso.
4. Desative a empresa e confira que todos os logins dos usuários vinculados ficam bloqueados e que a fornecedora ainda consegue consultar as OSs. Reative a empresa e confirme que as contas ativas conseguem fazer um novo login.

## Administração da equipe fornecedora

1. Entre com a conta **Consultor ADM**, abra **Equipe fornecedora** e crie um usuário. Em outra janela, entre com essa nova conta e confirme o acesso às ordens da fornecedora.
2. Em **Meu perfil**, mude o nome da fornecedora e o nome exibido do perfil pela conta administradora. Atualize a tela da segunda conta para conferir os nomes. O formulário **Identidade da fornecedora** aparece apenas para a conta administradora.
3. Com a segunda conta em uso, volte à tela **Equipe fornecedora** e edite ou desative esse usuário. Confira o bloqueio da sessão e, depois da reativação, o acesso por um novo login.

## Filtros de solicitantes e responsáveis

Na lista de OSs, selecione um **Solicitante** e confira as ordens abertas por ele. Selecione mais um e confirme que aparecem as ordens de qualquer um dos dois. Depois, selecione um ou mais **Responsáveis** e confirme que só aparecem as ordens que atendem aos dois filtros. Use **Limpar seleção** em cada grupo para voltar a consultar todas as pessoas. Repita no Kanban com o perfil da fornecedora.

## Indicadores personalizados

Na conta da fornecedora, abra **Indicadores** abaixo de **Aprovações de cadastro**. Crie o cartão **OSs em execução**, escolhendo o critério correspondente. Volte a **Ordens de serviço** e compare a contagem com as OSs em execução. Aplique um filtro de sistema e confira a atualização dos cartões. Como cliente, o mesmo cartão conta apenas as OSs da própria empresa.

Em **Indicadores cadastrados**, edite o nome ou o critério de um cartão personalizado e, em seguida, de um cartão padrão. Volte a **Ordens de serviço** e confira se os cartões existentes passaram a usar os dados salvos.

## Teste HTTP integrado

Após preparar as contas, use Node.js na pasta da solução. Defina as três senhas no ambiente do seu terminal e rode `node infra/validar.mjs`. As variáveis são `TAKTOS_TEST_FORNECEDORA`, `TAKTOS_TEST_ALFA` e `TAKTOS_TEST_BETA`. O teste usa dados fictícios identificados com o horário da execução. Ele cobre a criação de uma empresa, de sistemas, de contas e de OSs. O GitHub Actions prepara um banco temporário e executa esse mesmo cenário.
