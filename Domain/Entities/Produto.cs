namespace MinhaApi.Domain.Entities;

public class Produto
{
    public int Id { get; private set; }
    public string Nome { get; private set; }
    public decimal Preco { get; private set; }

    // Construtor orientado a DDD (Garante consistência do objeto)
    public Produto(string nome, decimal preco)
    {
        if (string.IsNullOrWhiteSpace(nome)) 
            throw new ArgumentException("O nome do produto é obrigatório.");
            
        if (preco < 0) 
            throw new ArgumentException("O preço não pode ser negativo.");

        Nome = nome;
        Preco = preco;
    }
}
