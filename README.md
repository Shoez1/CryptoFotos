# CryptoFotos

CryptoFotos e um utilitario WinForms para Windows que criptografa e descriptografa fotos e imagens em lote.

## O que foi reforcado

- Novos arquivos usam criptografia autenticada com `AES-GCM`, nonce aleatorio por arquivo e validacao contra adulteracao.
- Arquivos antigos continuam compativeis durante a descriptografia.
- O login agora aceita senha com hash `PBKDF2-SHA256`.
- A tela de login aplica bloqueio temporario apos repetidas tentativas invalidas.
- Os fluxos de criptografar, descriptografar e galeria preservam subpastas.
- O app evita sobrescrever silenciosamente diretorios de saida existentes.
- O projeto agora usa `net8.0-windows`, que ainda recebe atualizacoes de seguranca.

## Build rapido

Rode na raiz do projeto:

```bat
build-exe.bat
```

Fluxo do build:

- pergunta se deve sobrescrever o `login.txt`
- pede usuario
- pede senha
- pede dica de senha
- executa `dotnet restore`
- executa `dotnet publish`

Saida esperada:

`bin\Release\net8.0-windows\win-x64\publish\CryptoFotos.exe`

## Formato recomendado do login

```txt
version:1.5
usuario:admin
salt:BASE64_DO_SALT
senhahash:BASE64_DO_HASH_PBKDF2_SHA256
iteracoes:210000
dicadesenha:sua dica opcional
```

Voce pode partir de `login.example.txt`.

## Compatibilidade

- O projeto ainda aceita entradas legadas no formato `usuario:senha` para nao quebrar fluxos antigos.
- Arquivos antigos continuam podendo ser descriptografados.
- Novos arquivos passam a ser gravados no formato autenticado.

## Build manual

```powershell
dotnet restore -r win-x64
dotnet publish .\CryptoFotos.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Observacoes de seguranca

- A chave principal ainda fica embutida no binario, o que ajuda na usabilidade, mas nao substitui gestao real de segredos.
- `senhapadrao:sim` continua disponivel apenas por compatibilidade e e mais fraco do que usuario e senha com hash.
- Lotes muito grandes ainda sao processados em memoria, entao o uso de RAM pode crescer.
