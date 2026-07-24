using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Files.Infrastructure.Tests")]

// Castle DynamicProxy - нужен, чтобы NSubstitute мог создавать прокси для internal-типов этой
// сборки в тестах. Без PublicKey: Files.Infrastructure не подписана строгим именем, а
// InternalsVisibleTo для неподписанной сборки не может ссылаться на публичный ключ друга - по той
// же причине динамическая сборка Castle тоже останется неподписанной.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
