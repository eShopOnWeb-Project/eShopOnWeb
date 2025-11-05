"""change back to normal int id

Revision ID: cc406ec5107d
Revises: 37c6bdc06fa9
Create Date: 2025-09-25 12:58:57.721282

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'cc406ec5107d'
down_revision: Union[str, Sequence[str], None] = '37c6bdc06fa9'
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def downgrade() -> None:
    op.drop_table("catalogitem")
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


def upgrade() -> None:
    op.drop_table("catalogitem")
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