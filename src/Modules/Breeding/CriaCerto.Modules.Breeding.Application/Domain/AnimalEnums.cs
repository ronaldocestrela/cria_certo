namespace CriaCerto.Modules.Breeding.Application.Domain;

public enum CattleCategory
{
    Cow = 1,
    Bull = 2,
    Heifer = 3
}

public enum ReproductiveStatus
{
    Open = 1,          // Vazia
    InIatfProtocol = 2,// Em Protocolo IATF
    Inseminated = 3,   // Inseminada
    Pregnant = 4,      // Prenhe
    Culled = 5,        // Descartada
    Sold = 6,          // Vendida
    Active = 7         // Ativo / Apto (Reprodutor / Geral)
}

public enum DiagnosisMethod
{
    Ultrasound = 1,
    RectalPalpation = 2
}

public enum SemenType
{
    Conventional = 1,
    SexedFemale = 2,
    SexedMale = 3
}
