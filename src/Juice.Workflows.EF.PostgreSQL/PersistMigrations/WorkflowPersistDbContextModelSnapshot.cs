﻿// <auto-generated />
using Juice.Workflows.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Juice.Workflows.EF.PostgreSQL.PersistMigrations
{
    [DbContext(typeof(WorkflowPersistDbContext))]
    partial class WorkflowPersistDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.FlowSnapshot", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("WorkflowId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.HasKey("Id", "WorkflowId");

                    b.HasIndex("WorkflowId");

                    b.ToTable("FlowSnapshot", (string)null);
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.NodeSnapshot", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("WorkflowId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Message")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<string>("Outcomes")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<string>("User")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.HasKey("Id", "WorkflowId");

                    b.HasIndex("WorkflowId");

                    b.ToTable("NodeSnapshot", (string)null);
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.ProcessSnapshot", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("WorkflowId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.HasKey("Id", "WorkflowId");

                    b.HasIndex("WorkflowId");

                    b.ToTable("ProcessSnapshot", (string)null);
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.WorkflowState", b =>
                {
                    b.Property<string>("WorkflowId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Input")
                        .IsRequired()
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)");

                    b.Property<string>("LastMessages")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)");

                    b.Property<string>("NodeStates")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Output")
                        .IsRequired()
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)");

                    b.HasKey("WorkflowId");

                    b.ToTable("WorkflowState", (string)null);
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.FlowSnapshot", b =>
                {
                    b.HasOne("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.WorkflowState", null)
                        .WithMany("FlowSnapshots")
                        .HasForeignKey("WorkflowId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.NodeSnapshot", b =>
                {
                    b.HasOne("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.WorkflowState", null)
                        .WithMany("NodeSnapshots")
                        .HasForeignKey("WorkflowId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.ProcessSnapshot", b =>
                {
                    b.HasOne("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.WorkflowState", null)
                        .WithMany("ProcessSnapshots")
                        .HasForeignKey("WorkflowId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Juice.Workflows.Domain.AggregatesModel.WorkflowStateAggregate.WorkflowState", b =>
                {
                    b.Navigation("FlowSnapshots");

                    b.Navigation("NodeSnapshots");

                    b.Navigation("ProcessSnapshots");
                });
#pragma warning restore 612, 618
        }
    }
}
