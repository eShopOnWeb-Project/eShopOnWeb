"""changed catalog item id to GUID

Revision ID: 37c6bdc06fa9
Revises: 7418db84656c
Create Date: 2025-09-25 12:50:49.067763

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = '37c6bdc06fa9'
down_revision: Union[str, Sequence[str], None] = '7418db84656c'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.drop_table("catalogitem")  # only if safe
    op.create_table(
        "catalogitem",
        sa.Column("id", sa.UUID(as_uuid=True), primary_key=True, nullable=False, unique=True),
        sa.Column("name", sa.String(length=50), nullable=False),
        sa.Column("description", sa.Text, nullable=True),
        sa.Column("price", sa.Numeric(18, 2), nullable=False),
        sa.Column("picture_uri", sa.String, nullable=True),
        sa.Column("catalog_type_id", sa.Integer, sa.ForeignKey("catalogtype.id"), nullable=False),
        sa.Column("catalog_brand_id", sa.Integer, sa.ForeignKey("catalogbrand.id"), nullable=False),
    )


def downgrade() -> None:
    op.drop_table("catalogitem")
    # recreate with int PK if needed
    op.create_table(
        "catalogitem",
        sa.Column("id", sa.Integer, primary_key=True, nullable=False),
        sa.Column("name", sa.String(length=50), nullable=False),
        sa.Column("description", sa.Text, nullable=True),
        sa.Column("price", sa.Numeric(18, 2), nullable=False),
        sa.Column("picture_uri", sa.String, nullable=True),
        sa.Column("catalog_type_id", sa.Integer, sa.ForeignKey("catalogtype.id"), nullable=False),
        sa.Column("catalog_brand_id", sa.Integer, sa.ForeignKey("catalogbrand.id"), nullable=False),
    )
