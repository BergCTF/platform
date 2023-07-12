using Berg.ChallengeServer.Services;
using Berg.ChallengeServer.Configuration;

namespace Berg.ChallengeServer.Test.Services;

public class ScoringServiceTest
{

    [Test]
    public void TestTeamBasedScoring()
    {
        using var dbFactory = new BergDbContextFactory();
        using var db = dbFactory.CreateContext();
        var config = new CtfConfig
        {
            Teams = true,
            Scoring = new Scoring
            {
                MaximumScore = 500,
                MinimumScore = 100,
                NumSolvesBeforeMinimum = 4
            }
        };

        var scoringService = new ScoringService(config);
        scoringService.RefreshScores(db);
        Assert.Multiple(() =>
        {
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge1Id), Is.EqualTo(400));
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge2Id), Is.EqualTo(475));
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge3Id), Is.EqualTo(500));

            Assert.That(scoringService.GetTeamScore(BergDbContextFactory.Team1Id), Is.EqualTo(400));
            Assert.That(scoringService.GetTeamScore(BergDbContextFactory.Team2Id), Is.EqualTo(400 + 475));
        });
        var team1Solves = scoringService.GetTeamSolves(BergDbContextFactory.Team1Id);
        Assert.That(team1Solves, Has.Count.EqualTo(1));
        Assert.That(team1Solves[0].PlayerId, Is.EqualTo(BergDbContextFactory.Team1Player2Id));

        var team2Solves = scoringService.GetTeamSolves(BergDbContextFactory.Team2Id);
        Assert.That(team2Solves, Has.Count.EqualTo(2));

        var scoreboard = scoringService.GetTeamScoreboard();
        Assert.Multiple(() =>
        {
            Assert.That(scoreboard, Has.Count.EqualTo(3));
            Assert.That(scoreboard[0].TeamId, Is.EqualTo(BergDbContextFactory.Team2Id));
            Assert.That(scoreboard[1].TeamId, Is.EqualTo(BergDbContextFactory.Team1Id));
            Assert.That(scoreboard[2].TeamId, Is.EqualTo(BergDbContextFactory.Team3Id));
        });
    }

    [Test]
    public void TestIndividualScoring()
    {
        using var dbFactory = new BergDbContextFactory();
        using var db = dbFactory.CreateContext();
        var config = new CtfConfig
        {
            Teams = false,
            Scoring = new Scoring
            {
                MaximumScore = 500,
                MinimumScore = 100,
                NumSolvesBeforeMinimum = 4
            }
        };

        var scoringService = new ScoringService(config);
        scoringService.RefreshScores(db);
        Assert.Multiple(() =>
        {
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge1Id), Is.EqualTo(275));
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge2Id), Is.EqualTo(475));
            Assert.That(scoringService.GetChallengeValue(BergDbContextFactory.Challenge3Id), Is.EqualTo(500));

            Assert.That(scoringService.GetPlayerSolves(BergDbContextFactory.Team1Player1Id), Has.Count.EqualTo(1));
            Assert.That(scoringService.GetPlayerSolves(BergDbContextFactory.Team1Player2Id), Has.Count.EqualTo(1));
            Assert.That(scoringService.GetPlayerSolves(BergDbContextFactory.Team2Player1Id), Has.Count.EqualTo(2));

            Assert.That(scoringService.GetPlayerScore(BergDbContextFactory.Team1Player1Id), Is.EqualTo(275));
            Assert.That(scoringService.GetPlayerScore(BergDbContextFactory.Team1Player2Id), Is.EqualTo(275));
            Assert.That(scoringService.GetPlayerScore(BergDbContextFactory.Team2Player1Id), Is.EqualTo(275 + 475));
        });
        
        var scoreboard = scoringService.GetPlayerScoreboard();
        Assert.Multiple(() =>
        {
            Assert.That(scoreboard, Has.Count.EqualTo(3));
            Assert.That(scoreboard[0].PlayerId, Is.EqualTo(BergDbContextFactory.Team2Player1Id));
            Assert.That(scoreboard[1].PlayerId, Is.EqualTo(BergDbContextFactory.Team1Player2Id));
            Assert.That(scoreboard[2].PlayerId, Is.EqualTo(BergDbContextFactory.Team1Player1Id));
        });
    }
}