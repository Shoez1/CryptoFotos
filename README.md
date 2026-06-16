# CryptoFotos

CryptoFotos é um utilitário WinForms para Windows que criptografa e descriptografa fotos e imagens em lote.

## O que foi reforçado

- Arquivos usam o padrão único `CSG3`: AES-GCM autenticado, nonce aleatório por arquivo e validação contra adulteração.
- A chave padrão é embutida no executável e é a mesma em CryptoFotos, CryptoMulti e CryptoTxt.
- A chave de criptografia pode ser exportada/importada no padrão `CSK3`, sem senha no arquivo de chave.
- O login aceita apenas senha com hash `PBKDF2-SHA256`.
- A tela de login aplica bloqueio temporário após repetidas tentativas inválidas.
- Os fluxos de criptografar, descriptografar e galeria preservam subpastas.
- O app evita sobrescrever silenciosamente arquivos de saída existentes.
- O app ignora redirecionamentos de sistema de arquivos e aplica limites de quantidade, tamanho e dimensões de imagem.
- O app identifica fotos por extensão e assinatura quando disponível, incluindo formatos modernos e RAW de câmeras.
- O projeto agora usa `net8.0-windows`, que ainda recebe atualizações de segurança.

## Formatos de foto reconhecidos

O CryptoFotos é exclusivo para fotos/imagens e reconhece os principais formatos usados em câmeras, celulares e editores:

- Comuns: `jpg`, `jpeg`, `jpe`, `jfif`, `pjpeg`, `pjp`, `png`, `apng`, `bmp`, `dib`, `gif`, `tif`, `tiff`.
- Modernos/web: `webp`, `heic`, `heif`, `heics`, `heifs`, `hif`, `avif`, `avifs`, `jp2`, `j2k`, `jpf`, `jpx`, `jpm`, `mj2`, `jxl`.
- RAW/camera: `dng`, `cr2`, `cr3`, `nef`, `nrw`, `arw`, `srf`, `sr2`, `orf`, `rw2`, `raf`, `pef`, `srw`, `x3f`, `erf`, `kdc`, `dcr`, `mos`, `mrw`, `mef`, `iiq`, `3fr`, `fff`, `rwl`, `raw`.
- Edição fotográfica: `psd`, `psb`.

Formatos que o Windows Forms não consegue decodificar podem ser criptografados e restaurados normalmente, mas podem não gerar miniatura na galeria sem codec/suporte do Windows.

## Build Rápido

Rode na raiz do projeto:

```bat
build-exe.bat
```

Fluxo do build:

- pergunta se deve sobrescrever o `login.txt`
- pede usuário
- pede senha
- pede dica de senha
- executa `dotnet restore`
- executa `dotnet publish`

Saída esperada:

`bin\Release\net8.0-windows\win-x64\publish\CryptoFotos.exe`

O build falha se qualquer DLL ou arquivo extra ficar ao lado do executável final.

## Formato recomendado do login

```txt
version:1.5
usuario:admin
salt:BASE64_DO_SALT
senhahash:BASE64_DO_HASH_PBKDF2_SHA256
iteracoes:600000
dicadesenha:sua dica opcional
```

Você pode partir de `login.example.txt`.

Novos logins gerados pelos scripts usam `600000` iterações por padrão. O app rejeita `senhapadrao:sim` e o formato legado `usuario:senha`.

## Chave de criptografia

- A chave padrão fica incorporada ao executável e é igual nos três projetos.
- Use `Exportar Chave` para criar um backup `CSK3` sem senha.
- Use `Importar Chave` para abrir arquivos criptografados em outra instalação ou com uma chave compartilhada.
- Use `Gerar Chave` para criar uma chave totalmente nova e carregá-la na sessão atual; depois use `Exportar Chave` para salvar essa chave.
- Quando uma chave importada ou gerada estiver ativa, clicar em `Chave Importada`/`Chave Gerada` desativa essa chave e volta para a chave padrão embutida.

## Padrão Único

- O login legado em texto puro não é mais aceito.
- Não há fallback para formatos antigos de criptografia.
- Arquivos são gravados e lidos somente no formato autenticado `CSG3`.
- Arquivos de chave são importados somente no formato `CSK3`.

## Build manual

```powershell
dotnet restore .\CryptoFotos.csproj -r win-x64
dotnet publish .\CryptoFotos.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

## Observações de Segurança

- O arquivo de chave exportado permite acesso aos dados criptografados por quem possuir esse arquivo.
- Lotes muito grandes ainda são processados arquivo a arquivo em memória, mas agora há limites de tamanho, quantidade e dimensões.
