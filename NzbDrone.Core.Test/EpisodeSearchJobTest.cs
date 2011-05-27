﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoMoq;
using FizzWare.NBuilder;
using MbUnit.Framework;
using Moq;
using NzbDrone.Core.Model;
using NzbDrone.Core.Model.Notification;
using NzbDrone.Core.Providers;
using NzbDrone.Core.Providers.Indexer;
using NzbDrone.Core.Providers.Jobs;
using NzbDrone.Core.Repository;
using NzbDrone.Core.Repository.Quality;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test
{
    [TestFixture]
    // ReSharper disable InconsistentNaming
    public class EpisodeSearchJobTest : TestBase
    {
        [Test]
        public void ParseResult_should_return_after_match()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(5)
                .Build();

            var episode = Builder<Episode>.CreateNew().Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.IsAny<EpisodeParseResult>())).Returns(true);


            mocker.GetMock<DownloadProvider>()
                .Setup(c => c.DownloadReport(It.IsAny<EpisodeParseResult>())).Returns(true);
                

            //Act
            mocker.Resolve<EpisodeSearchJob>().ProcessResults(new ProgressNotification("Test"), episode, parseResults);

            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.IsAny<EpisodeParseResult>()), Times.Once());
            mocker.GetMock<DownloadProvider>().Verify(c => c.DownloadReport(It.IsAny<EpisodeParseResult>()), Times.Once());
        }


        [Test]
        public void higher_quality_should_be_called_first()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(2)
                .WhereTheFirst(1).Has(c => c.Quality = QualityTypes.Bluray1080p)
                .AndTheNext(1).Has(c => c.Quality = QualityTypes.DVD)
                .Build();

            var episode = Builder<Episode>.CreateNew().Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(parseResults[0])).Returns(true);

            mocker.GetMock<DownloadProvider>()
                .Setup(c => c.DownloadReport(parseResults[0])).Returns(true);

            //Act
            mocker.Resolve<EpisodeSearchJob>().ProcessResults(new ProgressNotification("Test"), episode, parseResults);

            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(parseResults[0]), Times.Once());
            mocker.GetMock<DownloadProvider>().Verify(c => c.DownloadReport(parseResults[0]), Times.Once());
        }


        [Test]
        public void when_same_quality_proper_should_be_called_first()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(20)
                .WhereAll().Have(c => c.Quality = QualityTypes.DVD)
                .And(c => c.Proper = false)
               .WhereRandom(1).Has(c => c.Proper = true)
                .Build();

            Assert.Count(1, parseResults.Where(c => c.Proper));

            var episode = Builder<Episode>.CreateNew().Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.Is<EpisodeParseResult>(p => p.Proper))).Returns(true);

            mocker.GetMock<DownloadProvider>()
                .Setup(c => c.DownloadReport(It.Is<EpisodeParseResult>(p => p.Proper))).Returns(true);


            //Act
            mocker.Resolve<EpisodeSearchJob>().ProcessResults(new ProgressNotification("Test"), episode, parseResults);

            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.Is<EpisodeParseResult>(p => p.Proper)), Times.Once());
            mocker.GetMock<DownloadProvider>().Verify(c => c.DownloadReport(It.Is<EpisodeParseResult>(p => p.Proper)), Times.Once());
        }


        [Test]
        public void when_not_needed_should_check_the_rest()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(4)
                .Build();

            var episode = Builder<Episode>.CreateNew().Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.IsAny<EpisodeParseResult>())).Returns(false);

            //Act
            mocker.Resolve<EpisodeSearchJob>().ProcessResults(new ProgressNotification("Test"), episode, parseResults);

            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.IsAny<EpisodeParseResult>()), Times.Exactly(4));
            ExceptionVerification.ExcpectedWarns(1);
        }


        [Test]
        public void failed_IsNeeded_should_check_the_rest()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(4)
                .Build();

            var episode = Builder<Episode>.CreateNew().Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.IsAny<EpisodeParseResult>())).Throws(new Exception());

            //Act
            mocker.Resolve<EpisodeSearchJob>().ProcessResults(new ProgressNotification("Test"), episode, parseResults);

            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.IsAny<EpisodeParseResult>()), Times.Exactly(4));
            ExceptionVerification.ExcpectedErrors(4);
            ExceptionVerification.ExcpectedWarns(1);
        }


        [Test]
        [Row(0)]
        [Row(-1)]
        [Row(-100)]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void target_id_less_than_0_throws_exception(int target)
        {
            var mocker = new AutoMoqer(MockBehavior.Strict);
            mocker.Resolve<EpisodeSearchJob>().Start(new ProgressNotification("Test"), target);
        }



        [Test]
        public void should_search_all_providers()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(4)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(c => c.Series = Builder<Series>.CreateNew().Build())
                .With(c => c.SeasonNumber = 12)
                .Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<EpisodeProvider>()
                .Setup(c => c.GetEpisode(episode.EpisodeId))
                .Returns(episode);

            var indexer1 = new Mock<IndexerBase>();
            indexer1.Setup(c => c.FetchEpisode(episode.Series.Title, episode.SeasonNumber, episode.EpisodeNumber))
                .Returns(parseResults).Verifiable();


            var indexer2 = new Mock<IndexerBase>();
            indexer2.Setup(c => c.FetchEpisode(episode.Series.Title, episode.SeasonNumber, episode.EpisodeNumber))
                .Returns(parseResults).Verifiable();

            var indexers = new List<IndexerBase> { indexer1.Object, indexer2.Object };

            mocker.GetMock<IndexerProvider>()
                .Setup(c => c.GetEnabledIndexers())
                .Returns(indexers);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.IsAny<EpisodeParseResult>())).Returns(false);

            //Act
            mocker.Resolve<EpisodeSearchJob>().Start(new ProgressNotification("Test"), episode.EpisodeId);


            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.IsAny<EpisodeParseResult>()), Times.Exactly(8));
            ExceptionVerification.ExcpectedWarns(1);
            indexer1.VerifyAll();
            indexer2.VerifyAll();
        }


        [Test]
        public void failed_indexer_should_not_break_job()
        {
            var parseResults = Builder<EpisodeParseResult>.CreateListOfSize(4)
                .Build();

            var episode = Builder<Episode>.CreateNew()
                .With(c => c.Series = Builder<Series>.CreateNew().Build())
                .With(c => c.SeasonNumber = 12)
                .Build();

            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<EpisodeProvider>()
                .Setup(c => c.GetEpisode(episode.EpisodeId))
                .Returns(episode);

            var indexer1 = new Mock<IndexerBase>();
            indexer1.Setup(c => c.FetchEpisode(episode.Series.Title, episode.SeasonNumber, episode.EpisodeNumber))
                .Returns(parseResults).Verifiable();


            var indexer2 = new Mock<IndexerBase>();
            indexer2.Setup(c => c.FetchEpisode(episode.Series.Title, episode.SeasonNumber, episode.EpisodeNumber))
                .Throws(new Exception()).Verifiable();

            var indexer3 = new Mock<IndexerBase>();
            indexer2.Setup(c => c.FetchEpisode(episode.Series.Title, episode.SeasonNumber, episode.EpisodeNumber))
                .Returns(parseResults).Verifiable();


            var indexers = new List<IndexerBase> { indexer1.Object, indexer2.Object, indexer3.Object };

            mocker.GetMock<IndexerProvider>()
                .Setup(c => c.GetEnabledIndexers())
                .Returns(indexers);

            mocker.GetMock<InventoryProvider>()
                .Setup(c => c.IsNeeded(It.IsAny<EpisodeParseResult>())).Returns(false);

            //Act
            mocker.Resolve<EpisodeSearchJob>().Start(new ProgressNotification("Test"), episode.EpisodeId);


            //Assert
            mocker.VerifyAllMocks();
            mocker.GetMock<InventoryProvider>().Verify(c => c.IsNeeded(It.IsAny<EpisodeParseResult>()), Times.Exactly(8));

            ExceptionVerification.ExcpectedWarns(1);
            ExceptionVerification.ExcpectedErrors(1);
            indexer1.VerifyAll();
            indexer2.VerifyAll();
            indexer3.VerifyAll();
        }



        [Test]
        public void no_episode_found_should_return_with_error_logged()
        {
            var mocker = new AutoMoqer(MockBehavior.Strict);

            mocker.GetMock<EpisodeProvider>()
                .Setup(c => c.GetEpisode(It.IsAny<long>()))
                .Returns<Episode>(null);

            //Act
            mocker.Resolve<EpisodeSearchJob>().Start(new ProgressNotification("Test"), 12);


            //Assert
            mocker.VerifyAllMocks();
            ExceptionVerification.ExcpectedErrors(1);
        }

    }
}
