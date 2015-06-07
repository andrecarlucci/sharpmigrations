namespace Sharp.Migrations {
    public interface ISeedRunner {
        void Run(string seedName, string param = null);        
    }
}