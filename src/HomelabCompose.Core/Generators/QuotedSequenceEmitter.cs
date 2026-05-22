using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace HomelabCompose.Core.Generators;

/// <summary>
/// Forces double-quoting on all string scalars inside YAML sequences.
/// Covers command flags, ports, labels, healthcheck test, volumes, etc.
/// </summary>
public class QuotedSequenceEmitter : ChainedEventEmitter
{
    private int _sequenceDepth;

    public QuotedSequenceEmitter(IEventEmitter nextEmitter) : base(nextEmitter) { }

    public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
    {
        _sequenceDepth++;
        base.Emit(eventInfo, emitter);
    }

    public override void Emit(SequenceEndEventInfo eventInfo, IEmitter emitter)
    {
        _sequenceDepth--;
        base.Emit(eventInfo, emitter);
    }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (_sequenceDepth > 0 && eventInfo.Source.Type == typeof(string))
            eventInfo.Style = ScalarStyle.DoubleQuoted;

        base.Emit(eventInfo, emitter);
    }
}
