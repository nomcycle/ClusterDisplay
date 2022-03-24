using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.ClusterDisplay.RepeaterStateMachine;
using static Unity.ClusterDisplay.Tests.NetworkingUtils;
using static Unity.ClusterDisplay.Tests.NodeTestUtils;

namespace Unity.ClusterDisplay.Tests
{
    public class RepeaterNodeTests
    {
        MockClusterSync m_ClusterSync;
        UDPAgent m_TestAgent;
        NodeState m_State;
        const byte k_EmitterId = 0;
        const byte k_RepeaterId = 1;

        [SetUp]
        public void SetUp()
        {
            m_ClusterSync = new MockClusterSync(MockClusterSync.NodeType.Repeater, k_RepeaterId);

            var testConfig = MockClusterSync.udpConfig;
            testConfig.nodeId = k_EmitterId;
            testConfig.rxPort = MockClusterSync.udpConfig.txPort;
            testConfig.txPort = MockClusterSync.udpConfig.rxPort;

            m_TestAgent = new UDPAgent(testConfig);
        }

        [Test]
        public void TestRegisterWithEmitter()
        {
            // Create the state under test
            var registerState = new RegisterWithEmitter(m_ClusterSync)
            {
                MaxTimeOut = TimeSpan.FromSeconds(MockClusterSync.timeoutSeconds)
            };

            var allNodesMask = m_ClusterSync.LocalNode.UdpAgent.AllNodesMask;
            Assert.Zero(allNodesMask & ((ulong) 1 << k_EmitterId));

            // Before receiving a WelcomeRepeater, we should be staying in this state
            registerState.EnterState(null);
            Assert.That(registerState.ProcessFrame(true), Is.EqualTo(registerState));

            // The state should be broadcasting HelloEmitter messages
            var (header, rolePublication) = m_TestAgent.ReceiveMessage<RolePublication>();
            Assert.That(header.MessageType, Is.EqualTo(EMessageType.HelloEmitter));
            Assert.That(rolePublication.NodeRole, Is.EqualTo(ENodeRole.Repeater));

            // ReadyToProceed should be false. This state does not allow advancement of frames
            Assert.IsFalse(registerState.ReadyToProceed);

            // Send an acceptance message
            m_TestAgent.PublishMessage(new MessageHeader
            {
                MessageType = EMessageType.WelcomeRepeater,
                DestinationIDs = (ulong) 1 << header.OriginID,
            });

            // Wait for the state to transition
            m_State = RunStateUntilTransition(registerState);
            Assert.That(m_State, Is.TypeOf<RepeaterSynchronization>());

            allNodesMask = m_ClusterSync.LocalNode.UdpAgent.AllNodesMask;
            Assert.NotZero(allNodesMask & ((ulong) 1 << k_EmitterId));
        }

        [UnityTest]
        public IEnumerator TestRepeaterSynchronization()
        {
            var repeaterSynchronization = new RepeaterSynchronization(m_ClusterSync);
            repeaterSynchronization.EnterState(null);

            // Simulate several frames
            const int kNumFrames = 4;
            for (var frameNum = 0ul; frameNum < kNumFrames; frameNum++)
            {
                m_ClusterSync.CurrentFrameID = frameNum;

                // The state should waiting for the emitter data that marks the sync point
                // at th beginning of the frame
                Assert.False(repeaterSynchronization.ReadyToProceed);
                Assert.That(repeaterSynchronization.Stage, Is.EqualTo(RepeaterSynchronization.EStage.WaitingOnEmitterFrameData));

                var (header, lastFrameMsg) = GenerateMessage(k_EmitterId,
                    new byte[] {k_RepeaterId},
                    EMessageType.LastFrameData,
                    new EmitterLastFrameData()
                    {
                        FrameNumber = frameNum
                    },
                    MessageHeader.EFlag.Broadcast,
                    new byte[] {0}); // trailing 0 to indicate empty state data

                m_TestAgent.PublishMessage(header, lastFrameMsg);

                Assert.IsTrue(RunStateUntilReady(repeaterSynchronization));

                // This is where a new frame would start in a real cluster
                yield return null;
                m_State = repeaterSynchronization.ProcessFrame(newFrame: true);
                Assert.That(repeaterSynchronization.Stage, Is.EqualTo(RepeaterSynchronization.EStage.EnteredNextFrame));

                // Repeater signals start of new frame and waits for ack
                m_State = repeaterSynchronization.ProcessFrame(false);
                Assert.That(repeaterSynchronization.Stage, Is.EqualTo(RepeaterSynchronization.EStage.WaitForEmitterACK));

                // Expect to receive a signal that node as entered a new frame
                var receiveMessage = m_TestAgent
                    .ReceiveMessageAsync<RepeaterEnteredNextFrame>()
                    .ToCoroutine();

                yield return receiveMessage.WaitForCompletion(MockClusterSync.timeoutSeconds);
                Assert.True(receiveMessage.IsSuccessful);
                var (rxHeader, contents) = receiveMessage.Result;
                Assert.That(rxHeader.MessageType, Is.EqualTo(EMessageType.EnterNextFrame));

                // Now wait for the state to transition back to WaitingOnEmitterData
                Assert.IsTrue(RunStateUtil(repeaterSynchronization,
                    state => state.Stage == RepeaterSynchronization.EStage.WaitingOnEmitterFrameData));
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Awkward way to invoke m_State.ExitState
            new NullState(m_ClusterSync).EnterState(m_State);
            
            m_ClusterSync.LocalNode.Exit();
            m_TestAgent.Stop();
        }
    }
}