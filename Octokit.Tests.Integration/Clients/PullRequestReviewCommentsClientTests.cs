﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
using Octokit.Tests.Integration;
using Xunit;

public class PullRequestReviewCommentsClientTests : IDisposable
{
    readonly IGitHubClient _gitHubClient;
    readonly IPullRequestReviewCommentsClient _client;
    readonly Repository _repository;

    const string branchName = "new-branch";
    const string branchHead = "heads/" + branchName;
    const string branchRef = "refs/" + branchHead;
    const string path = "CONTRIBUTING.md";

    public PullRequestReviewCommentsClientTests()
    {
        _gitHubClient = new GitHubClient(new ProductHeaderValue("OctokitTests"))
        {
            Credentials = Helper.Credentials
        };

        _client = _gitHubClient.PullRequest.Comment;

        // We'll create a pull request that can be used by most tests
        var repoName = Helper.MakeNameWithTimestamp("test-repo");
        _repository = CreateRepository(repoName).Result;
    }

    [IntegrationTest]
    public async Task CanCreateAndRetrieveComment()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const string body = "A review comment message";
        const int position = 1;

        var createdComment = await CreateComment(body, position, pullRequest.Sha, pullRequest.Number);

        var commentFromGitHub = await _client.GetComment(Helper.UserName, _repository.Name, createdComment.Id);

        AssertComment(commentFromGitHub, body, position);
    }

    [IntegrationTest]
    public async Task CanEditComment()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const string body = "A new review comment message";
        const int position = 1;

        var createdComment = await CreateComment(body, position, pullRequest.Sha, pullRequest.Number);

        var edit = new PullRequestReviewCommentEdit("Edited Comment");

        var editedComment = await _client.Edit(Helper.UserName, _repository.Name, createdComment.Id, edit);

        var commentFromGitHub = await _client.GetComment(Helper.UserName, _repository.Name, editedComment.Id);

        AssertComment(commentFromGitHub, edit.Body, position);
    }

    [IntegrationTest]
    public async Task TimestampsAreUpdated()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const string body = "A new review comment message";
        const int position = 1;

        var createdComment = await CreateComment(body, position, pullRequest.Sha, pullRequest.Number);

        Assert.Equal(createdComment.UpdatedAt, createdComment.CreatedAt);

        await Task.Delay(TimeSpan.FromSeconds(2));

        var edit = new PullRequestReviewCommentEdit("Edited Comment");

        var editedComment = await _client.Edit(Helper.UserName, _repository.Name, createdComment.Id, edit);

        Assert.NotEqual(editedComment.UpdatedAt, editedComment.CreatedAt);
    }

    [IntegrationTest]
    public async Task CanDeleteComment()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const string body = "A new review comment message";
        const int position = 1;

        var createdComment = await CreateComment(body, position, pullRequest.Sha, pullRequest.Number);

        Assert.DoesNotThrow(async () => { await _client.Delete(Helper.UserName, _repository.Name, createdComment.Id); });
    }

    [IntegrationTest]
    public async Task CanCreateReply()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const string body = "Reply me!";
        const int position = 1;

        var createdComment = await CreateComment(body, position, pullRequest.Sha, pullRequest.Number);

        var reply = new PullRequestReviewCommentReplyCreate("Replied", createdComment.Id);
        var createdReply = await _client.CreateReply(Helper.UserName, _repository.Name, pullRequest.Number, reply);
        var createdReplyFromGitHub = await _client.GetComment(Helper.UserName, _repository.Name, createdReply.Id);

        AssertComment(createdReplyFromGitHub, reply.Body, position);
    }

    [IntegrationTest]
    public async Task CanGetForPullRequest()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const int position = 1;
        var commentsToCreate = new List<string> { "Comment 1", "Comment 2", "Comment 3" };

        await CreateComments(commentsToCreate, position, _repository.Name, pullRequest.Sha, pullRequest.Number);

        var pullRequestComments = await _client.GetAll(Helper.UserName, _repository.Name, pullRequest.Number);

        AssertComments(pullRequestComments, commentsToCreate, position);
    }

    [IntegrationTest]
    public async Task CanGetForRepository()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const int position = 1;
        var commentsToCreate = new List<string> { "Comment One", "Comment Two" };

        await CreateComments(commentsToCreate, position, _repository.Name, pullRequest.Sha, pullRequest.Number);

        var pullRequestComments = await _client.GetForRepository(Helper.UserName, _repository.Name);

        AssertComments(pullRequestComments, commentsToCreate, position);
    }

    [IntegrationTest]
    public async Task CanGetForRepositoryAscendingSort()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const int position = 1;
        var commentsToCreate = new [] { "Comment One", "Comment Two", "Comment Three" };

        await CreateComments(commentsToCreate, position, _repository.Name, pullRequest.Sha, pullRequest.Number);

        var pullRequestComments = await _client.GetForRepository(Helper.UserName, _repository.Name, new PullRequestReviewCommentRequest { Direction = SortDirection.Ascending });

        Assert.Equal(pullRequestComments.Select(x => x.Body), commentsToCreate);
    }

    [IntegrationTest]
    public async Task CanGetForRepositoryDescendingSort()
    {
        var pullRequest = await CreatePullRequest(_repository);

        const int position = 1;
        var commentsToCreate = new [] { "Comment One", "Comment Two", "Comment Three", "Comment Four" };

        await CreateComments(commentsToCreate, position, _repository.Name, pullRequest.Sha, pullRequest.Number);

        var pullRequestComments = await _client.GetForRepository(Helper.UserName, _repository.Name, new PullRequestReviewCommentRequest { Direction = SortDirection.Descending });

        Assert.Equal(pullRequestComments.Select(x => x.Body), commentsToCreate.Reverse());
    }

    public void Dispose()
    {
        Helper.DeleteRepo(_repository);
    }

    async Task<PullRequestReviewComment> CreateComment(string body, int position, string commitId, int number)
    {
        return await CreateComment(body, position, _repository.Name, commitId, number);
    }

    async Task<PullRequestReviewComment> CreateComment(string body, int position, string repoName, string pullRequestCommitId, int pullRequestNumber)
    {
        var comment = new PullRequestReviewCommentCreate(body, pullRequestCommitId, path, position);

        var createdComment = await _client.Create(Helper.UserName, repoName, pullRequestNumber, comment);

        AssertComment(createdComment, body, position);

        return createdComment;
    }

    async Task CreateComments(IEnumerable<string> comments, int position, string repoName, string pullRequestCommitId, int pullRequestNumber)
    {
        foreach (var comment in comments)
        {
            await CreateComment(comment, position, repoName, pullRequestCommitId, pullRequestNumber);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    static void AssertComment(PullRequestReviewComment comment, string body, int position)
    {
        Assert.NotNull(comment);
        Assert.Equal(body, comment.Body);
        Assert.Equal(position, comment.Position);
    }

    static void AssertComments(IReadOnlyList<PullRequestReviewComment> comments, List<string> bodies, int position)
    {
        Assert.Equal(bodies.Count, comments.Count);

        for (var i = 0; i < bodies.Count; i = i + 1)
        {
            AssertComment(comments[i], bodies[i], position);
        }
    }

    async Task<Repository> CreateRepository(string repoName)
    {
        return await _gitHubClient.Repository.Create(new NewRepository { Name = repoName, AutoInit = true });
    }

    /// <summary>
    /// Creates the base state for testing (creates a repo, a commit in master, a branch, a commit in the branch and a pull request)
    /// </summary>
    /// <returns></returns>
    async Task<PullRequestData> CreatePullRequest(Repository repository)
    {
        var repoName = repository.Name;

        // Creating a commit in master

        var createdCommitInMaster = await CreateCommit(repoName, "Hello World!", "README.md", "heads/master", "A master commit message");

        // Creating a branch

        var newBranch = new NewReference(branchRef, createdCommitInMaster.Sha);
        await _gitHubClient.GitDatabase.Reference.Create(Helper.UserName, repoName, newBranch);

        // Creating a commit in the branch

        var createdCommitInBranch = await CreateCommit(repoName, "Hello from the fork!", path, branchHead, "A branch commit message");

        // Creating a pull request

        var pullRequest = new NewPullRequest("Nice title for the pull request", branchName, "master");
        var createdPullRequest = await _gitHubClient.PullRequest.Create(Helper.UserName, repoName, pullRequest);

        var data = new PullRequestData
        {
            Sha = createdCommitInBranch.Sha,
            Number = createdPullRequest.Number,
        };

        return data;
    }

    async Task<Commit> CreateCommit(string repoName, string blobContent, string treePath, string reference, string commitMessage)
    {
        // Creating a blob
        var blob = new NewBlob
        {
            Content = blobContent,
            Encoding = EncodingType.Utf8
        };

        var createdBlob = await _gitHubClient.GitDatabase.Blob.Create(Helper.UserName, repoName, blob);

        // Creating a tree
        var newTree = new NewTree();
        newTree.Tree.Add(new NewTreeItem
        {
            Type = TreeType.Blob,
            Mode = FileMode.File,
            Path = treePath,
            Sha = createdBlob.Sha,
        });

        var createdTree = await _gitHubClient.GitDatabase.Tree.Create(Helper.UserName, repoName, newTree);
        var treeSha = createdTree.Sha;

        // Creating a commit
        var parent = await _gitHubClient.GitDatabase.Reference.Get(Helper.UserName, repoName, reference);
        var commit = new NewCommit(commitMessage, treeSha, parent.Object.Sha);

        var createdCommit = await _gitHubClient.GitDatabase.Commit.Create(Helper.UserName, repoName, commit);
        await _gitHubClient.GitDatabase.Reference.Update(Helper.UserName, repoName, reference, new ReferenceUpdate(createdCommit.Sha));

        return createdCommit;
    }

    class PullRequestData
    {
        public int Number { get; set; }
        public string Sha { get; set; }
    }
}
