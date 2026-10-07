using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoGuard.Contracts;

namespace CryptoGuard.ML;

public sealed class ForestNode
{
    public int Feature { get; set; } = -1;
    public double Threshold { get; set; }
    public int Left { get; set; } = -1;
    public int Right { get; set; } = -1;
    public double? SuspiciousFraction { get; set; }
}
public sealed class ForestModel
{
    public string Format { get; set; } = "sentinelzone-forest-json-1";
    public string ModelVersion { get; set; } = "";
    public string FeatureContractVersion { get; set; } = ReleaseVersions.Features;
    public string[] FeatureNames { get; set; } = [];
    public double[] TrainingMedians { get; set; } = [];
    public List<ForestNode[]> Trees { get; set; } = [];
}
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.SnakeCaseLower,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ForestModel))]
[JsonSerializable(typeof(Dictionary<string,FeatureValue>))]
public partial class MlJson : JsonSerializerContext;

// Read-only numeric trees. No pickle, executable model graph, Python runtime or native inference dependency.
public sealed class ShadowForest
{
    private readonly ForestModel model;
    private ShadowForest(ForestModel model)=>this.model=model;
    public static ShadowForest Load(string path,string expectedSha256)
    {
        if(expectedSha256.Length!=64 || !expectedSha256.All(Uri.IsHexDigit) || new FileInfo(path).Length>16*1024*1024)throw new InvalidDataException("model_integrity_configuration");
        var bytes=File.ReadAllBytes(path);
        if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("model_hash_mismatch");
        var m=JsonSerializer.Deserialize(bytes,MlJson.Default.ForestModel) ?? throw new InvalidDataException("model_json");
        if(m.Format!="sentinelzone-forest-json-1" || string.IsNullOrWhiteSpace(m.ModelVersion) || m.FeatureContractVersion!=ReleaseVersions.Features ||
            m.FeatureNames is null || !m.FeatureNames.SequenceEqual(Features.Names) || m.TrainingMedians is null || m.TrainingMedians.Length!=Features.Names.Length || m.TrainingMedians.Any(x=>!double.IsFinite(x)) || m.Trees is null || m.Trees.Count is <1 or >256)
            throw new InvalidDataException("model_contract");
        foreach(var tree in m.Trees)
        {
            if(tree is null || tree.Length is <1 or >8191 || tree.Any(n=>n is null))throw new InvalidDataException("model_tree_size");
            var visited=new HashSet<int>();
            void Check(int i,int depth)
            {
                if(i<0 || i>=tree.Length || depth>32 || !visited.Add(i))throw new InvalidDataException("model_tree_graph");
                var n=tree[i];
                if(n.SuspiciousFraction is double leaf) { if(!double.IsFinite(leaf) || leaf<0 || leaf>1)throw new InvalidDataException("model_leaf");return; }
                if(n.Feature<0 || n.Feature>=Features.Names.Length || !double.IsFinite(n.Threshold))throw new InvalidDataException("model_split");
                Check(n.Left,depth+1);Check(n.Right,depth+1);
            }
            Check(0,0);
            if(visited.Count!=tree.Length)throw new InvalidDataException("model_unreachable_node");
        }
        return new(m);
    }
    public MlShadowResult Predict(Dictionary<string,FeatureValue> features)
    {
        var values=Features.Names.Select((name,i)=>features.TryGetValue(name,out var f) && f.Status=="ok" && f.Value is double value && double.IsFinite(value)?value:model.TrainingMedians[i]).ToArray();
        double coverage=(double)Features.Names.Count(name=>features.TryGetValue(name,out var f) && f.Status=="ok" && f.Value is double v && double.IsFinite(v))/Features.Names.Length;
        if(coverage<0.5)return new() { Enabled=true,ModelVersion=model.ModelVersion,FeatureCoverage=coverage,Status="insufficient_feature_coverage" };
        double sum=0;
        foreach(var tree in model.Trees)
        {
            int index=0;
            // sklearn trees evaluate float32 inputs; match that conversion on both native runtimes.
            while(tree[index].SuspiciousFraction is null) { var n=tree[index];index=(float)values[n.Feature]<=n.Threshold?n.Left:n.Right; }
            sum+=tree[index].SuspiciousFraction!.Value;
        }
        var score=sum/model.Trees.Count;
        return new() { Enabled=true,ModelVersion=model.ModelVersion,FeatureCoverage=coverage,Status="ok",Score=score,Prediction=score>=0.5?"suspicious":"normal" };
    }
}
