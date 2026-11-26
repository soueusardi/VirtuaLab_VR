namespace VirtuaLab
{
    /// <summary>
    /// Tipos de componentes de hardware reconhecidos pelo validador de encaixe.
    /// Para adicionar uma peça nova: acrescente um valor AO FINAL deste enum
    /// (não reordene: os valores são serializados como inteiros nas cenas).
    /// </summary>
    public enum TipoComponente
    {
        PlacaMae = 0,
        Processador = 1,
        PastaTermica = 2,
        Cooler = 3,
        MemoriaRAM = 4,
        SSD = 5,
        PlacaDeVideo = 6,
        Fonte = 7,
    }

    /// <summary>Resultado da validação de uma peça contra um ponto de encaixe.</summary>
    public enum ResultadoEncaixe
    {
        /// <summary>Peça correta, pré-requisitos atendidos, dentro da tolerância: pode encaixar.</summary>
        Valido,
        /// <summary>Peça correta, mas ainda fora do raio de encaixe (não é erro).</summary>
        ForaDeAlcance,
        /// <summary>Peça de tipo diferente do aceito pelo ponto (erro de local).</summary>
        TipoIncorreto,
        /// <summary>Peça correta, mas falta instalar outra etapa antes (erro de ordem).</summary>
        PreRequisitoPendente,
        /// <summary>Peça correta e próxima, mas girada além da tolerância angular (erro de orientação).</summary>
        OrientacaoIncorreta,
    }

    /// <summary>Estado visual do guia (fantasma) de um ponto de encaixe.</summary>
    public enum EstadoGuia
    {
        Oculto,
        /// <summary>Azul translúcido: indica o destino da peça segurada.</summary>
        Destino,
        /// <summary>Verde: soltar agora encaixa a peça.</summary>
        Valido,
        /// <summary>Vermelho: soltar agora será rejeitado.</summary>
        Invalido,
    }
}
