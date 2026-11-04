using MuscleCuties.UseCases.Features.Cycle.Contracts;
using CoreCyclePhase = MuscleCuties.Core.Models.Enums.Cycle.CyclePhase;
using CoreFlowIntensity = MuscleCuties.Core.Models.Enums.Cycle.FlowIntensity;
using CoreSymptomType = MuscleCuties.Core.Models.Enums.Cycle.SymptomType;

namespace MuscleCuties.Core.Tests;

public sealed class CycleContractMapperTests
{
    [Fact]
    public void Phase_Contracts_Map_In_Both_Directions()
    {
        var mappings = new[]
        {
            (CyclePhaseDto.Menstrual, CoreCyclePhase.Menstrual),
            (CyclePhaseDto.Follicular, CoreCyclePhase.Follicular),
            (CyclePhaseDto.Ovulatory, CoreCyclePhase.Ovulatory),
            (CyclePhaseDto.Luteal, CoreCyclePhase.Luteal),
            (CyclePhaseDto.Unknown, CoreCyclePhase.Unknown)
        };

        foreach (var (contract, core) in mappings)
        {
            Assert.Equal(core, CycleContractMapper.ToCore(contract));
            Assert.Equal(contract, CycleContractMapper.ToDto(core));
        }
    }

    [Fact]
    public void Flow_Contracts_Map_In_Both_Directions()
    {
        var mappings = new[]
        {
            (FlowIntensityDto.None, CoreFlowIntensity.None),
            (FlowIntensityDto.Spotting, CoreFlowIntensity.Spotting),
            (FlowIntensityDto.Light, CoreFlowIntensity.Light),
            (FlowIntensityDto.Medium, CoreFlowIntensity.Medium),
            (FlowIntensityDto.Heavy, CoreFlowIntensity.Heavy)
        };

        foreach (var (contract, core) in mappings)
        {
            Assert.Equal(core, CycleContractMapper.ToCore(contract));
            Assert.Equal(contract, CycleContractMapper.ToDto(core));
        }
    }

    [Fact]
    public void Symptoms_Map_In_Both_Directions()
    {
        var mappings = new[]
        {
            (SymptomTypeDto.Cramps, CoreSymptomType.Cramps),
            (SymptomTypeDto.Bloating, CoreSymptomType.Bloating),
            (SymptomTypeDto.Fatigue, CoreSymptomType.Fatigue),
            (SymptomTypeDto.Headache, CoreSymptomType.Headache),
            (SymptomTypeDto.MoodSwings, CoreSymptomType.MoodSwings),
            (SymptomTypeDto.Spotting, CoreSymptomType.Spotting),
            (SymptomTypeDto.Other, CoreSymptomType.Other),
            (SymptomTypeDto.BreastTenderness, CoreSymptomType.BreastTenderness),
            (SymptomTypeDto.LowerBackPain, CoreSymptomType.LowerBackPain),
            (SymptomTypeDto.Acne, CoreSymptomType.Acne),
            (SymptomTypeDto.Nausea, CoreSymptomType.Nausea),
            (SymptomTypeDto.Cravings, CoreSymptomType.Cravings)
        };

        foreach (var (contract, core) in mappings)
        {
            Assert.Equal(core, CycleContractMapper.ToCore(contract));
            Assert.Equal(contract, CycleContractMapper.ToDto(core));
        }
    }
}
