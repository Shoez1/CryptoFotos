# CryptoFotos

CryptoFotos é um utilitário moderno para Windows que permite criptografar e descriptografar fotos e imagens de forma simples, segura e portátil.

## Funcionalidades
- **Criptografia forte (AES-256)**: Apenas o programa pode descriptografar as imagens.
- **Proteção por login**: O acesso ao programa é restrito por usuário e senha, definidos antes do build.
- **Interface intuitiva**: Basta selecionar a pasta de imagens e clicar em "Criptografar" ou "Descriptografar".
- **Executável portátil**: Após o build, o programa roda como um único `.exe` sem necessidade de arquivos externos.
- **Ícone personalizado**: O executável possui ícone próprio (crypt.ico).

## Como usar

### 1. Defina o login e senha
Edite o arquivo `login.txt` na raiz do projeto antes de compilar. Exemplo:
```
admin:123456
dicadesenha:sua dica aqui
```
Adicione a linha começando com `dicadesenha:` para exibir uma dica na tela de login. Exemplo:
```
admin:123456
dicadesenha:admin
```

#### Modo de senha dinâmica (opcional)
Se quiser ativar um modo de senha que muda automaticamente conforme o dia e a hora, adicione a linha abaixo como primeira linha do `login.txt`:
```
senhapadrao:sim
```
Neste modo, **apenas o campo de senha será exibido** na tela de login. A senha correta será:
- **(dia do mês + 1)** seguido de **(hora atual - 1)**, tudo junto.
- Exemplo: Se a data/hora for 13/08/25 18:55, a senha será `1417` (13+1=14, 18-1=17).
- A dica exibida será `+d-h`.
Para voltar ao modo tradicional, basta remover ou trocar para `senhapadrao:nao`.

### 2. (Opcional) Troque a chave de criptografia
Para maior segurança, você pode alterar a chave e o IV (vetor de inicialização) usados na criptografia:
- Abra o arquivo `Utils/CryptoUtils.cs`.
- Substitua os valores dos arrays `key` (32 bytes) e `iv` (16 bytes) por outros valores aleatórios.
- Exemplo de como gerar novos valores:
  - Use um gerador de bytes aleatórios seguro, como o site [random.org](https://www.random.org/bytes/) ou um script em Python/C#.
- **Atenção:** Só imagens criptografadas com a mesma chave/IV poderão ser descriptografadas pelo programa.

### 3. Compile o executável portátil
Execute:
```sh
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
```
O executável estará em `bin/Release/net6.0-windows/win-x64/publish/CryptoFotos.exe`.

### 4. Rode o programa
- Dê dois cliques no `CryptoFotos.exe`.
- Faça login com o usuário e senha definidos.
- Selecione uma pasta de imagens (formatos suportados: .jpg, .jpeg, .png, .bmp, .gif).
- Clique em "Criptografar" para gerar uma nova pasta com as imagens criptografadas (sufixo `-cry`).
- Clique em "Descriptografar" para restaurar as imagens originais a partir da pasta criptografada.
- Use "Visualizar Galeria" para pré-visualizar miniaturas das imagens criptografadas.

### 5. Portabilidade
Você pode copiar apenas o `CryptoFotos.exe` para qualquer computador com Windows 10/11 e rodar normalmente.

## Observações Técnicas
- O login é embutido no executável como recurso, não ficando exposto após o build.
- A chave de criptografia é fixa e interna ao código.
- O programa mostra mensagens de erro claras caso haja problemas com o login ou arquivos.
- O ícone do executável é definido por `crypt.ico` (adicione ou substitua antes do build).

## Segurança
- Apenas quem possui o programa consegue descriptografar as imagens.
- Recomenda-se não compartilhar o `.exe` se quiser manter as imagens protegidas.

## Licença
MIT

---

Desenvolvido com ❤️ para uso pessoal e educacional. Se encontrar bugs ou quiser contribuir, abra uma issue ou pull request!
