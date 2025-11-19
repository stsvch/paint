namespace WpfClient;

public static class DatabaseConfig
{
    public const string Server = "localhost";
    public const int Port = 3308;
    public const string Database = "paintdb";
    public const string User = "paintuser";
    public const string Password = "paintpassword";

    public static string ConnectionString =>
        $"Server={Server};Port={Port};Database={Database};User ID={User};Password={Password};CharSet=utf8mb4;";
}

